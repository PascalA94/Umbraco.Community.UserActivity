using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.IntegrationTests.Core;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Core;
using Umbraco.Community.UserActivity.Sources.Forms;

namespace Umbraco.Community.UserActivity.IntegrationTests.Forms;

/// <summary>
/// The Forms source over real <c>umbracoLog</c> rows shaped as S2 recorded them. Forms isn't installed in the test
/// host, so a minimal <c>UFForms</c> (the columns we read) stands in where a test needs names.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class FormsSourceTests : UmbracoIntegrationTest
{
    private static readonly DateTime T0 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ContactKey = Guid.Parse("c0000000-0000-4000-8000-000000000001");

    private IScopeProvider Scopes => GetRequiredService<IScopeProvider>();

    private FormsSource Source()
        => new(Scopes, new TestOptionsMonitor<UserActivityOptions>(new UserActivityOptions()), _ => true);

    private static ActivitySourceQuery Query(ResolvedUser? user = null, string? search = null, string[]? categories = null, string[]? entityTypes = null, int limit = 50)
        => new(user, new HashSet<string>(categories ?? []), new HashSet<string>(entityTypes ?? []),
            new DateTimeOffset(T0.AddDays(-1)), new DateTimeOffset(T0.AddDays(1)), search, limit, false);

    private void CreateFormsTable(params (int NodeId, Guid Key, string Name)[] forms)
    {
        using IScope scope = Scopes.CreateScope();
        scope.Database.Execute("CREATE TABLE UFForms (id INTEGER PRIMARY KEY, \"Key\" TEXT NOT NULL, Name TEXT NOT NULL, NodeId INTEGER NULL)");
        foreach ((int nodeId, Guid key, string name) in forms)
        {
            scope.Database.Execute("INSERT INTO UFForms (\"Key\", Name, NodeId) VALUES (@0, @1, @2)", key, name, nodeId);
        }

        scope.Complete();
    }

    private void SeedFormActions()
    {
        // S2's sequence for one form (NodeId 12) by user -1, plus a core row that must stay core's.
        CoreSeed.Log(Scopes, -1, 12, "Form", "New", null, T0);
        CoreSeed.Log(Scopes, -1, 12, "Form", "Save", null, T0.AddMinutes(1));
        CoreSeed.Log(Scopes, -1, 12, "Form", "Move", "Moved to recycle bin", T0.AddMinutes(2), "trash");
        CoreSeed.Log(Scopes, -1, 12, "Form", "Move", "Restored from recycle bin", T0.AddMinutes(3), "restore");
        CoreSeed.Log(Scopes, -1, 12, "Form", "RollBack", "Rolled back to version c0000000-0000-4000-8000-0000000000ff", T0.AddMinutes(4), "c0000000-0000-4000-8000-0000000000ff");
        CoreSeed.Log(Scopes, -1, 13, "Form", "Delete", null, T0.AddMinutes(5));
        CoreSeed.Log(Scopes, -1, -1, "Document", "Save", "core row", T0.AddMinutes(6));
    }

    [Test]
    public async Task FormRows_Only_WithNamesFromUFForms_DeletedFormNamed()
    {
        CreateFormsTable((12, ContactKey, "Contact us"));
        SeedFormActions();

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(6));
        Assert.That(page.Total, Is.EqualTo(6));
        Assert.That(page.Items.Select(i => i.Category), Is.EqualTo(new[]
        {
            Categories.Deleted, Categories.RolledBack, Categories.Restored, Categories.Trashed, Categories.Saved, Categories.Created,
        }));
        ActivityEvent save = page.Items.Single(i => i.Category == Categories.Saved);
        Assert.That(save.Entity, Is.EqualTo(new ActivityEntity("form", ContactKey.ToString("D"), "12", "Contact us", true)));
        Assert.That(page.Items[0].Entity, Is.EqualTo(new ActivityEntity("form", null, "13", "Deleted form (id 13)", false)));
    }

    [Test]
    public async Task NoFormsTable_FailsSoft()
    {
        SeedFormActions();

        ActivitySourcePage page = await Source().QueryAsync(Query(search: "recycle"), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(2));
        Assert.That(page.Items.Select(i => i.Entity!.Name), Is.All.Null);
    }

    [TestCase(Categories.Trashed, 1)]
    [TestCase(Categories.Restored, 1)]
    [TestCase(Categories.Moved, 0)]
    [TestCase(Categories.Saved, 1)]
    [TestCase(Categories.Published, 0)]
    public async Task CategoryFilter_SplitsMoveByParameters(string category, int expected)
    {
        SeedFormActions();
        CoreSeed.Log(Scopes, -1, 12, "Form", "Move", "Moved", T0.AddMinutes(7), null);

        ActivitySourcePage page = await Source().QueryAsync(Query(categories: [category]), CancellationToken.None);

        int extraMove = category == Categories.Moved ? 1 : 0;
        Assert.That(page.Items, Has.Count.EqualTo(expected + extraMove));
        Assert.That(page.Items.Select(i => i.Category), Is.All.EqualTo(category));
        Assert.That(page.Total, Is.EqualTo(expected + extraMove));
    }

    [Test]
    public async Task CategoryFilter_Other_And_EntityFilter()
    {
        SeedFormActions();
        CoreSeed.Log(Scopes, -1, 12, "Form", "Custom", "x", T0.AddMinutes(8));

        Assert.That((await Source().QueryAsync(Query(categories: [Categories.Other]), CancellationToken.None)).Items.Select(i => i.RawType), Is.EqualTo(new[] { "Custom" }));
        Assert.That((await Source().QueryAsync(Query(entityTypes: ["document"]), CancellationToken.None)).Items, Is.Empty);
        Assert.That((await Source().QueryAsync(Query(entityTypes: ["form"]), CancellationToken.None)).Items, Has.Count.EqualTo(7));
    }

    [Test]
    public async Task UserFilter_ById()
    {
        Umbraco.Cms.Core.Models.Membership.IUser wren = await new TestUsers(Services).CreateAsync("Wren Writer", ["writer"]);
        SeedFormActions();
        CoreSeed.Log(Scopes, wren.Id, 12, "Form", "Save", null, T0.AddMinutes(9));

        ActivitySourcePage page = await Source().QueryAsync(Query(new ResolvedUser(wren.Key, wren.Id, wren.Username, null, null)), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.Actor.UserId), Is.EqualTo(new int?[] { wren.Id }));
    }

    [Test]
    public async Task Search_MatchesFormName()
    {
        CreateFormsTable((12, ContactKey, "Contact us"));
        SeedFormActions();

        Assert.That((await Source().QueryAsync(Query(search: "Contact"), CancellationToken.None)).Items, Has.Count.EqualTo(5));
        Assert.That((await Source().QueryAsync(Query(search: "%"), CancellationToken.None)).Items, Is.Empty);
    }

    [Test]
    public async Task CoreSkipsFormRows_WithTheRealClaim()
    {
        SeedFormActions();
        var core = new CoreSource(Scopes, new CoreClaims(() => [Source()]), new TestOptionsMonitor<UserActivityOptions>(new UserActivityOptions()));

        ActivitySourcePage page = await core.QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items.Where(i => i.Id.StartsWith("log:", StringComparison.Ordinal)).Select(i => i.Summary), Is.EqualTo(new[] { "core row" }));
    }

    [Test]
    public async Task GetAsync_FormRowOnly()
    {
        CreateFormsTable((12, ContactKey, "Contact us"));
        SeedFormActions();
        ActivitySourcePage page = await Source().QueryAsync(Query(categories: [Categories.Trashed]), CancellationToken.None);

        ActivityEvent? found = await Source().GetAsync(page.Items.Single().Id, CancellationToken.None);

        Assert.That(found!.Details!["parameters"], Is.EqualTo("trash"));
        Assert.That(found.Entity!.Name, Is.EqualTo("Contact us"));
        int coreId;
        using (IScope scope = Scopes.CreateScope(autoComplete: true))
        {
            coreId = scope.Database.ExecuteScalar<int>("SELECT id FROM umbracoLog WHERE entityType = 'Document' AND logComment = 'core row'");
        }

        Assert.That(await Source().GetAsync($"log:{coreId}", CancellationToken.None), Is.Null, "the core Document row");
        Assert.That(await Source().GetAsync("audit:1", CancellationToken.None), Is.Null);
    }
}
