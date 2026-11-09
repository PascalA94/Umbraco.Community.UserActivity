using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NPoco;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.IntegrationTests.Core;

[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class CoreLogSourceTests : UmbracoIntegrationTest
{
    private static readonly DateTime T0 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private readonly List<IClaimsCoreAuditRows> _claims = [];

    private IScopeProvider Scopes => GetRequiredService<IScopeProvider>();

    private TestUsers TestUsers => new(Services);

    [SetUp]
    public void ClearClaims() => _claims.Clear();

    private CoreSource Source(UserActivityOptions? options = null)
        => new(Scopes, new CoreClaims(() => _claims), new TestOptionsMonitor<UserActivityOptions>(options ?? new UserActivityOptions()));

    private static ActivitySourceQuery Query(
        ResolvedUser? user = null,
        DateTime? from = null,
        DateTime? to = null,
        string? search = null,
        int limit = 50,
        bool ascending = false,
        string[]? categories = null,
        string[]? entityTypes = null)
        => new(
            user,
            new HashSet<string>(categories ?? []),
            new HashSet<string>(entityTypes ?? []),
            new DateTimeOffset(from ?? T0.AddDays(-1)),
            new DateTimeOffset(to ?? T0.AddDays(1)),
            search,
            limit,
            ascending);

    private static ResolvedUser Resolved(IUser u) => new(u.Key, u.Id, u.Username, u.Email, null);

    private void Log(int? userId, string header, string? comment = null, int nodeId = 1, string? entityType = "Document", DateTime? at = null, string? parameters = null)
        => CoreSeed.Log(Scopes, userId, nodeId, entityType, header, comment, at ?? T0, parameters);

    private async Task<Content> CreatePageAsync(string name)
    {
        IContentType contentType = ContentTypeBuilder.CreateBasicContentType();
        await GetRequiredService<IContentTypeService>().CreateAsync(contentType, Umbraco.Cms.Core.Constants.Security.SuperUserKey);
        Content content = ContentBuilder.CreateBasicContent(contentType);
        content.Name = name;
        GetRequiredService<IContentService>().Save(content);
        return content;
    }

    [Test]
    public async Task ByUserId_ReturnsOnlyThatUser()
    {
        IUser wren = await TestUsers.CreateAsync("Wren Writer", ["writer"]);
        IUser ed = await TestUsers.CreateEditorAsync("Ed Editor");
        IAuditService audit = GetRequiredService<IAuditService>();
        audit.Add(AuditType.Save, wren.Id, 1, "Document", "Saved by Wren");
        audit.Add(AuditType.Publish, wren.Id, 1, "Document", "Published by Wren");
        audit.Add(AuditType.Move, ed.Id, 1, "Document", "Moved by Ed");
        audit.Add(AuditType.Delete, ed.Id, 1, "Document", "Deleted by Ed");

        ActivitySourcePage page = await Source().QueryAsync(
            Query(Resolved(wren), DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1)), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.RawType), Is.EquivalentTo(new[] { "Publish", "Save" }));
        Assert.That(page.Items.All(i => i.Actor.UserId == wren.Id && i.Actor.Kind == ActorKind.User), Is.True);
        Assert.That(page.Items.Select(i => i.Category), Is.EquivalentTo(new[] { Categories.Published, Categories.Saved }));
        Assert.That(page.Items.All(i => i.Source == "core" && i.Id.StartsWith("log:", StringComparison.Ordinal)), Is.True);
        Assert.That(page.Items.All(i => i.IpAddress is null), Is.True);
    }

    [Test]
    public async Task DateWindow_Inclusive()
    {
        Log(-1, "Save", "before", at: T0.AddSeconds(-1));
        Log(-1, "Save", "at-from", at: T0);
        Log(-1, "Save", "at-to", at: T0.AddHours(1));
        Log(-1, "Save", "after", at: T0.AddHours(1).AddSeconds(1));

        ActivitySourcePage page = await Source().QueryAsync(Query(from: T0, to: T0.AddHours(1)), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.Summary), Is.EqualTo(new[] { "at-to", "at-from" }));
        Assert.That(page.Items[0].OccurredUtc, Is.EqualTo(new DateTimeOffset(T0.AddHours(1))));
        Assert.That(page.Items[0].OccurredUtc.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public async Task JoinsNodeName_AndFlagsDeleted()
    {
        Content content = await CreatePageAsync("Live page");
        Log(-1, "Publish", nodeId: content.Id, at: T0);
        Log(-1, "Delete", nodeId: 987654, at: T0.AddMinutes(1));

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        ActivityEntity live = page.Items.Single(i => i.RawType == "Publish").Entity!;
        Assert.That(live.Name, Is.EqualTo("Live page"));
        Assert.That(live.Key, Is.EqualTo(content.Key.ToString("D")));
        Assert.That(live.Type, Is.EqualTo("document"));
        Assert.That(live.Exists, Is.True);
        ActivityEntity gone = page.Items.Single(i => i.RawType == "Delete").Entity!;
        Assert.That(gone.Name, Is.EqualTo("Deleted item (id 987654)"));
        Assert.That(gone.Exists, Is.False);
        Assert.That(gone.Key, Is.Null);
    }

    [Test]
    public async Task NonNodeEntity_NeverBorrowsNodeName()
    {
        // umbracoLog.NodeId holds a user id for entityType User: joining umbracoNode by id alone would show an unrelated node's name.
        Content content = await CreatePageAsync("Some page");
        Log(-1, "Custom", "x", nodeId: content.Id, entityType: "User");
        Log(-1, "Save", "y", nodeId: content.Id, entityType: "Media");

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        ActivityEntity user = page.Items.Single(i => i.Summary == "x").Entity!;
        Assert.That(user.Name, Is.Null);
        Assert.That(user.Type, Is.EqualTo("user"));
        Assert.That(user.Id, Is.EqualTo(content.Id.ToString()));
        ActivityEntity media = page.Items.Single(i => i.Summary == "y").Entity!;
        Assert.That(media.Name, Is.EqualTo($"Deleted item (id {content.Id})"));
        Assert.That(media.Exists, Is.False);
    }

    [Test]
    public async Task CoresOwnSaveRow_ResolvesNodeName()
    {
        // QA-12: the entityType string core itself writes maps to the node, not just our hand-seeded strings.
        Content content = await CreatePageAsync("Core wrote this");

        ActivitySourcePage page = await Source().QueryAsync(Query(from: DateTime.UtcNow.AddHours(-1), to: DateTime.UtcNow.AddHours(1)), CancellationToken.None);

        ActivityEvent save = page.Items.First(i => i.Entity?.Id == content.Id.ToString());
        Assert.That(save.Entity!.Type, Is.EqualTo("document"));
        Assert.That(save.Entity.Name, Is.EqualTo("Core wrote this"));
        Assert.That(save.Entity.Exists, Is.True);
    }

    [Test]
    public async Task NonNodeTypes_LanguageAndUser_NoNameNoDeletedClaim_AndSearchSkipsTheJoin()
    {
        // QA-4/QA-5: Language (an UmbracoObjectTypes value but not an umbracoNode row) and User rows whose NodeId
        // equals a real node's id neither show nor match that node's name.
        Content content = await CreatePageAsync("Board minutes");
        Log(-1, "Save", "language row", nodeId: content.Id, entityType: "Language");
        Log(-1, "Custom", "user row", nodeId: content.Id, entityType: "User");

        ActivitySourcePage all = await Source().QueryAsync(Query(), CancellationToken.None);
        ActivitySourcePage search = await Source().QueryAsync(Query(search: "Board minutes", from: T0.AddDays(-1), to: T0.AddDays(1)), CancellationToken.None);

        ActivityEntity language = all.Items.Single(i => i.Summary == "language row").Entity!;
        Assert.That(language.Name, Is.Null);
        Assert.That(language.Exists, Is.True);
        Assert.That(search.Items.Select(i => i.Summary), Has.None.EqualTo("language row").And.None.EqualTo("user row"));
    }

    [Test]
    public async Task Search_TotalIsNotCounted()
    {
        Log(-1, "Save", "findme one");
        Log(-1, "Save", "findme two");

        ActivitySourcePage page = await Source().QueryAsync(Query(search: "findme"), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(2));
        Assert.That(page.Total, Is.Null, "a LIKE search is not counted (design 3.7, QA-2)");
    }

    [Test]
    public async Task MinusOneObject_ShowsFileNotRecorded()
    {
        Log(-1, "Save", nodeId: -1, entityType: "Stylesheet");

        ActivityEntity entity = (await Source().QueryAsync(Query(), CancellationToken.None)).Items.Single().Entity!;

        Assert.That(entity.Name, Is.EqualTo("File (name not recorded by Umbraco)"));
        Assert.That(entity.Type, Is.EqualTo("stylesheet"));
        Assert.That(entity.Exists, Is.False);
    }

    [Test]
    public async Task Search_EscapesWildcards()
    {
        Log(-1, "Save", "100% done");
        Log(-1, "Save", "1000 done");
        Log(-1, "Save", "a_b here");
        Log(-1, "Save", "axb here");
        Log(-1, "Save", "[x] bracket");
        Log(-1, "Save", "x bracket");
        Log(-1, "Save", "back\\slash");

        async Task<string?[]> Find(string s)
            => (await Source().QueryAsync(Query(search: s), CancellationToken.None)).Items.Select(i => i.Summary).ToArray();

        Assert.That(await Find("100%"), Is.EqualTo(new[] { "100% done" }));
        Assert.That(await Find("a_b"), Is.EqualTo(new[] { "a_b here" }));
        Assert.That(await Find("[x]"), Is.EqualTo(new[] { "[x] bracket" }));
        Assert.That(await Find("k\\s"), Is.EqualTo(new[] { "back\\slash" }));
        Assert.That(await Find("DONE"), Has.Length.EqualTo(2));
    }

    [Test]
    public async Task Search_MatchesParametersAndNodeName()
    {
        Content content = await CreatePageAsync("Findable page");
        Log(-1, "Save", "c1", nodeId: content.Id);
        Log(-1, "Save", "c2", parameters: "findable-param");
        Log(-1, "Save", "c3");

        ActivitySourcePage page = await Source().QueryAsync(Query(search: "findable"), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.Summary), Is.EquivalentTo(new[] { "c1", "c2" }));
    }

    [Test]
    public async Task ClaimedEntityType_SkippedByCoreLog()
    {
        Log(-1, "Save", "form row", entityType: "Form");
        Log(-1, "Save", "doc row");
        Log(-1, "Save", "no type", entityType: null);

        ActivitySourcePage unclaimed = await Source().QueryAsync(Query(), CancellationToken.None);
        _claims.Add(new Claims([], ["form"]));
        ActivitySourcePage claimed = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(unclaimed.Items.Select(i => i.Summary), Is.EquivalentTo(new[] { "form row", "doc row", "no type" }));
        Assert.That(claimed.Items.Select(i => i.Summary), Is.EquivalentTo(new[] { "doc row", "no type" }));
        Assert.That(claimed.Total, Is.EqualTo(2));
    }

    [Test]
    public async Task Total_Counts_WithSameFilters()
    {
        for (var i = 0; i < 5; i++)
        {
            Log(-1, "Publish", $"p{i}", at: T0.AddMinutes(i));
        }

        Log(-1, "Save", "s");
        Log(-1, "Publish", "old", at: T0.AddDays(-30));

        ActivitySourcePage page = await Source().QueryAsync(Query(limit: 2, categories: [Categories.Published]), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.Summary), Is.EqualTo(new[] { "p4", "p3" }));
        Assert.That(page.Total, Is.EqualTo(5));
        Assert.That(page.Truncated, Is.True);
    }

    [Test]
    public async Task Filters_CategoryOther_EntityType_Ascending()
    {
        Log(-1, "Custom", "custom", at: T0);
        Log(-1, "SomethingNew", "unknown header", at: T0.AddMinutes(1));
        Log(-1, "Save", "media save", entityType: "Media", at: T0.AddMinutes(2));
        Log(-1, "Save", "doc save", at: T0.AddMinutes(3));

        ActivitySourcePage other = await Source().QueryAsync(Query(categories: [Categories.Other], ascending: true), CancellationToken.None);
        ActivitySourcePage media = await Source().QueryAsync(Query(entityTypes: ["media"]), CancellationToken.None);
        ActivitySourcePage signIns = await Source().QueryAsync(Query(categories: [Categories.SignedIn]), CancellationToken.None);

        Assert.That(other.Items.Select(i => i.Summary), Is.EqualTo(new[] { "custom", "unknown header" }));
        Assert.That(media.Items.Select(i => i.Summary), Is.EqualTo(new[] { "media save" }));
        Assert.That(signIns.Items, Is.Empty);
        Assert.That(signIns.Total, Is.EqualTo(0));
    }

    [Test]
    public async Task NullAndSpecialUsers_MapToKinds()
    {
        Log(null, "Save", "null user");
        Log(-1, "Save", "default admin");

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items.Single(i => i.Summary == "null user").Actor.Kind, Is.EqualTo(ActorKind.System));
        Assert.That(page.Items.Single(i => i.Summary == "default admin").Actor.UserId, Is.EqualTo(-1));
    }

    [Test]
    public async Task GetAsync_ReturnsDetails_AndRejectsOtherIds()
    {
        Log(-1, "Save", "with detail", parameters: "p=1");
        ActivityEvent listed = (await Source().QueryAsync(Query(), CancellationToken.None)).Items.Single();

        ActivityEvent? detail = await Source().GetAsync(listed.Id, CancellationToken.None);

        Assert.That(detail, Is.Not.Null);
        Assert.That(detail!.Details!["comment"], Is.EqualTo("with detail"));
        Assert.That(detail.Details["parameters"], Is.EqualTo("p=1"));
        Assert.That(listed.Details, Is.Null);
        Assert.That(await Source().GetAsync("log:999999", CancellationToken.None), Is.Null);
        Assert.That(await Source().GetAsync("log:abc", CancellationToken.None), Is.Null);
        Assert.That(await Source().GetAsync("nope", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task GetAsync_ClaimedRow_IsNotCore()
    {
        Log(-1, "Save", "form row", entityType: "Form");
        string id = (await Source().QueryAsync(Query(), CancellationToken.None)).Items.Single().Id;
        _claims.Add(new Claims([], ["Form"]));

        Assert.That(await Source().GetAsync(id, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task Summary_CappedAt500()
    {
        Log(-1, "Save", new string('x', 800));

        ActivityEvent e = (await Source().QueryAsync(Query(), CancellationToken.None)).Items.Single();

        Assert.That(e.Summary!.Length, Is.EqualTo(ActivityEvent.MaxSummaryLength));
    }

    [Test]
    public void CoreSql_NoSelectStar()
    {
        ISqlContext ctx = Scopes.SqlContext;
        foreach (Sql<ISqlContext> sql in new[] { CoreSql.Log(ctx, Filter(), count: false), CoreSql.Log(ctx, Filter(), count: true), CoreSql.LogById(ctx, 1, []) })
        {
            Assert.That(Regex.IsMatch(sql.SQL, @"SELECT\s+\*|\.\*", RegexOptions.IgnoreCase), Is.False, sql.SQL);
        }
    }

    [Test]
    public void CoreSql_InjectionStrings_AreParameters()
    {
        string[] hostile = ["'; DROP TABLE umbracoLog;--", "%", "_", "["];
        foreach (var value in hostile)
        {
            Sql<ISqlContext> sql = CoreSql.Log(
                Scopes.SqlContext,
                Filter() with { Search = value, EntityTypes = [value], ExcludedEntityTypes = [value] },
                count: false);

            // SQL Server quotes identifiers with [ ]; strip quoting so only literal text is checked.
            string text = Regex.Replace(sql.SQL, @"\[(\w+)\]", "$1");
            Assert.That(text, Does.Not.Contain(value), sql.SQL);
            string[] args = sql.Arguments.SelectMany(a => a is string str ? [str] : a is IEnumerable<string> list ? list : []).ToArray();
            Assert.That(args.Any(a => a.Contains(value, StringComparison.Ordinal)), Is.True);
        }
    }

    private static CoreSql.LogFilter Filter() => new(T0, T0.AddDays(1), 7, ["save"], IncludeOtherHeaders: true, [], [], null, Ascending: false);

    private sealed class Claims(string[] prefixes, string[] entityTypes) : IClaimsCoreAuditRows
    {
        public IReadOnlyCollection<string> ClaimedAuditEventTypePrefixes => prefixes;

        public IReadOnlyCollection<string> ClaimedLogEntityTypes => entityTypes;
    }
}
