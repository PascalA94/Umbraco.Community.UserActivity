using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.ArchivedUsers;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.IntegrationTests.ArchivedUsers;

/// <summary>
/// Rows written exactly as Archived Users writes them: core's <c>IAuditService.AddAsync(AuditType.Custom, performer,
/// target id, "User", "ArchivedUsers: &lt;Action&gt; (&lt;Source&gt;)")</c>.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class ArchivedUsersSourceTests : UmbracoIntegrationTest
{
    private IScopeProvider Scopes => GetRequiredService<IScopeProvider>();

    private IAuditService Audit => GetRequiredService<IAuditService>();

    private TestUsers TestUsers => new(Services);

    private static TestOptionsMonitor<UserActivityOptions> Options() => new(new UserActivityOptions());

    private ArchivedUsersSource Source() => new(Scopes, Options(), _ => true);

    private static ActivitySourceQuery Query(ResolvedUser? user = null, string? search = null, string[]? categories = null, string[]? entityTypes = null)
        => new(user, new HashSet<string>(categories ?? []), new HashSet<string>(entityTypes ?? []),
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddMinutes(5), search, 50, false);

    private async Task<(IUser Sam, IUser Wren)> SeedAsync()
    {
        IUser sam = await TestUsers.CreateAdminAsync("Sam Admin");
        IUser wren = await TestUsers.CreateAsync("Wren Writer", ["writer"]);
        await Audit.AddAsync(AuditType.Custom, sam.Key, wren.Id, "User", "ArchivedUsers: Archived (Manual)");
        await Audit.AddAsync(AuditType.Custom, sam.Key, wren.Id, "User", "ArchivedUsers: Exported (Manual)");
        // Not 01's: another package's Custom row on a user, and core's own user row, stay with core.
        await Audit.AddAsync(AuditType.Custom, sam.Key, wren.Id, "User", "SomethingElse: hello");
        await Audit.AddAsync(AuditType.Save, sam.Key, wren.Id, "User", "core user save");
        return (sam, wren);
    }

    [Test]
    public async Task OnlyArchivedUsersRows_PerformerIsActor_TargetIsEntity()
    {
        (IUser sam, IUser wren) = await SeedAsync();

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.RawType), Is.EquivalentTo(new[] { "Archived", "Exported" }));
        Assert.That(page.Total, Is.EqualTo(2));
        Assert.That(page.Items.Select(i => i.Actor.UserId), Is.All.EqualTo(sam.Id));
        Assert.That(page.Items.Select(i => i.Entity!.Id), Is.All.EqualTo(wren.Id.ToString()));
    }

    [Test]
    public async Task UserFilter_IsThePerformer()
    {
        (IUser sam, IUser wren) = await SeedAsync();

        Assert.That((await Source().QueryAsync(Query(new ResolvedUser(sam.Key, sam.Id, sam.Username, null, null)), CancellationToken.None)).Items, Has.Count.EqualTo(2));
        Assert.That((await Source().QueryAsync(Query(new ResolvedUser(wren.Key, wren.Id, wren.Username, null, null)), CancellationToken.None)).Items, Is.Empty);
    }

    [Test]
    public async Task Categories_EntityTypes_Search()
    {
        await SeedAsync();

        Assert.That((await Source().QueryAsync(Query(categories: [Categories.Exported]), CancellationToken.None)).Items.Select(i => i.RawType), Is.EqualTo(new[] { "Exported" }));
        Assert.That((await Source().QueryAsync(Query(categories: [Categories.Other]), CancellationToken.None)).Items.Select(i => i.RawType), Is.EqualTo(new[] { "Archived" }));
        Assert.That((await Source().QueryAsync(Query(categories: [Categories.Saved]), CancellationToken.None)).Items, Is.Empty);
        Assert.That((await Source().QueryAsync(Query(entityTypes: ["document"]), CancellationToken.None)).Items, Is.Empty);
        Assert.That((await Source().QueryAsync(Query(search: "Archived (Man"), CancellationToken.None)).Items, Has.Count.EqualTo(1));
        Assert.That((await Source().QueryAsync(Query(search: "_"), CancellationToken.None)).Items, Is.Empty);
    }

    [Test]
    public async Task CoreSkipsTheClaimedRows_KeepsTheRest()
    {
        await SeedAsync();
        var core = new CoreSource(Scopes, new CoreClaims(() => [Source()]), Options());

        ActivitySourcePage page = await core.QueryAsync(Query(entityTypes: ["user"]), CancellationToken.None);

        var comments = page.Items.Where(i => i.Id.StartsWith("log:", StringComparison.Ordinal)).Select(i => i.Summary).ToList();
        Assert.That(comments, Has.None.StartsWith("ArchivedUsers:"));
        Assert.That(comments, Is.SupersetOf(new[] { "SomethingElse: hello", "core user save" }));
    }

    [Test]
    public async Task CoreKeepsEverything_WhenNotClaimed()
    {
        await SeedAsync();
        var core = new CoreSource(Scopes, new CoreClaims(() => []), Options());

        ActivitySourcePage page = await core.QueryAsync(Query(entityTypes: ["user"]), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.Summary), Has.Some.StartsWith("ArchivedUsers:"));
    }

    [Test]
    public async Task GetAsync_ClaimedRowOnly_BothWays()
    {
        await SeedAsync();
        var core = new CoreSource(Scopes, new CoreClaims(() => [Source()]), Options());
        ActivitySourcePage mine = await Source().QueryAsync(Query(), CancellationToken.None);
        ActivitySourcePage cores = await new CoreSource(Scopes, new CoreClaims(() => []), Options()).QueryAsync(Query(entityTypes: ["user"]), CancellationToken.None);
        string archivedId = mine.Items.Single(i => i.RawType == "Archived").Id;
        string otherId = cores.Items.Single(i => i.Summary == "SomethingElse: hello").Id;

        Assert.That((await Source().GetAsync(archivedId, CancellationToken.None))!.Details!["action"], Is.EqualTo("Archived"));
        Assert.That(await Source().GetAsync(otherId, CancellationToken.None), Is.Null);
        Assert.That(await core.GetAsync(archivedId, CancellationToken.None), Is.Null, "claimed rows are not core's, even by id");
        Assert.That(await core.GetAsync(otherId, CancellationToken.None), Is.Not.Null);
    }
}
