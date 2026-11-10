using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.IntegrationTests.Core;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Automate;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.IntegrationTests.Automate;

/// <summary>The Automate source over real <c>umbracoAudit</c> rows (shapes from S3, synthetic ids).</summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class AutomateSourceTests : UmbracoIntegrationTest
{
    private static readonly DateTime T0 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Sam = Guid.Parse("1f0c0000-0000-4000-8000-0000000000aa");
    private static readonly Guid Wren = Guid.Parse("1f0c0000-0000-4000-8000-0000000000cc");
    private static readonly Guid ServiceAccount = Guid.Parse("9a7e0000-0000-4000-8000-0000000000bb");

    private IScopeProvider Scopes => GetRequiredService<IScopeProvider>();

    private AutomateSource Source(bool showIp = false)
        => new(Scopes, new TestOptionsMonitor<UserActivityOptions>(new UserActivityOptions { ShowIpAddresses = showIp }), _ => true);

    private static ActivitySourceQuery Query(
        ResolvedUser? user = null, DateTime? from = null, DateTime? to = null, string? search = null, int limit = 50,
        string[]? categories = null, string[]? entityTypes = null)
        => new(user, new HashSet<string>(categories ?? []), new HashSet<string>(entityTypes ?? []),
            new DateTimeOffset(from ?? T0.AddDays(-1)), new DateTimeOffset(to ?? T0.AddDays(1)), search, limit, false);

    private static string Details(string initiatedBy, string run = "6f1c2a10-0000-4000-8000-000000000002")
        => $"{{\"automationId\":\"6f1c2a10-0000-4000-8000-000000000001\",\"runId\":\"{run}\",\"stepId\":\"6f1c2a10-0000-4000-8000-000000000003\",\"workspace\":\"Demo\",\"initiatedBy\":\"{initiatedBy}\"}}";

    private void SeedRun(string initiatedBy, DateTime at, string eventType = "umbraco/automate/umbracoAutomate/createContent")
        => CoreSeed.Audit(Scopes, 7, ServiceAccount, eventType, at, Details(initiatedBy));

    private void SeedCoreRows()
    {
        CoreSeed.Audit(Scopes, 7, ServiceAccount, "umbraco/user/save", T0.AddMinutes(1), affectedUserId: 3, affectedUserKey: Wren);
        CoreSeed.Audit(Scopes, -1, null, "umbraco/user/sign-in/login", T0.AddMinutes(2), affectedUserId: 3, affectedUserKey: Sam);
    }

    [Test]
    public async Task OnlyAutomateRows_AreReturned()
    {
        SeedCoreRows();
        SeedRun($"user:{Sam:D}", T0);

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(1));
        Assert.That(page.Items[0].RawType, Does.StartWith("umbraco/automate/"));
        Assert.That(page.Total, Is.EqualTo(1));
    }

    [Test]
    public async Task FilterByHumanKey_FindsRun()
    {
        SeedRun($"user:{Sam:D}", T0);
        SeedRun($"user:{Wren:D}", T0.AddMinutes(1));
        SeedRun("scheduled:6f1c2a10-0000-4000-8000-000000000001", T0.AddMinutes(2));

        ActivitySourcePage page = await Source().QueryAsync(Query(new ResolvedUser(Sam, 5, "sam", null, null)), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(1));
        Assert.That(page.Items[0].Actor.UserKey, Is.EqualTo(Sam));
        Assert.That(page.Items[0].Actor.Via!.UserKey, Is.EqualTo(ServiceAccount));
        Assert.That(page.Total, Is.EqualTo(1));
    }

    [Test]
    public async Task FilterByHumanKey_UpperCaseStoredKey_FindsRun()
    {
        SeedRun($"user:{Sam.ToString("D").ToUpperInvariant()}", T0);

        ActivitySourcePage page = await Source().QueryAsync(Query(new ResolvedUser(Sam, 5, "sam", null, null)), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(1));
        Assert.That(page.Items[0].Actor.UserKey, Is.EqualTo(Sam));
    }

    [Test]
    public async Task FilterByServiceAccount_FindsRun()
    {
        SeedRun($"user:{Sam:D}", T0);
        SeedRun("scheduled:6f1c2a10-0000-4000-8000-000000000001", T0.AddMinutes(1));

        ActivitySourcePage page = await Source().QueryAsync(Query(new ResolvedUser(ServiceAccount, 7, "automate", null, null)), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(2));
        Assert.That(page.Items.Select(i => i.Actor.Kind), Is.EquivalentTo(new[] { ActorKind.User, ActorKind.Automation }));
    }

    [Test]
    public async Task FilterByReplayedRunId_NeverMatchesAUser()
    {
        // A replay's id is a run id; even if it equals someone's key, it is not that person (S3).
        SeedRun($"replay:{Sam:D}", T0);

        ActivitySourcePage page = await Source().QueryAsync(Query(new ResolvedUser(Sam, 5, "sam", null, null)), CancellationToken.None);

        Assert.That(page.Items, Is.Empty);
    }

    [Test]
    public async Task Window_IsApplied()
    {
        SeedRun($"user:{Sam:D}", T0.AddDays(-3));
        SeedRun($"user:{Sam:D}", T0);
        SeedRun($"user:{Sam:D}", T0.AddDays(3));

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.OccurredUtc), Is.EqualTo(new[] { new DateTimeOffset(T0) }));
    }

    [Test]
    public async Task CategoryOrEntityFilter_WithoutRanOrAutomation_ReturnsNothing()
    {
        SeedRun($"user:{Sam:D}", T0);

        Assert.That((await Source().QueryAsync(Query(categories: [Categories.Saved]), CancellationToken.None)).Items, Is.Empty);
        Assert.That((await Source().QueryAsync(Query(entityTypes: ["document"]), CancellationToken.None)).Items, Is.Empty);
        Assert.That((await Source().QueryAsync(Query(categories: [Categories.Ran], entityTypes: ["automation"]), CancellationToken.None)).Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Search_MatchesEventTypeAndDetails_Escaped()
    {
        SeedRun($"user:{Sam:D}", T0, "umbraco/automate/umbracoAutomate/createContent");
        SeedRun($"user:{Sam:D}", T0.AddMinutes(1), "umbraco/automate/umbraco/http/request");

        Assert.That((await Source().QueryAsync(Query(search: "http/req"), CancellationToken.None)).Items, Has.Count.EqualTo(1));
        Assert.That((await Source().QueryAsync(Query(search: "Demo"), CancellationToken.None)).Items, Has.Count.EqualTo(2));
        Assert.That((await Source().QueryAsync(Query(search: "%_%"), CancellationToken.None)).Items, Is.Empty);
    }

    [Test]
    public async Task BadRow_IsSkipped_OthersReturned()
    {
        SeedRun($"user:{Sam:D}", T0);
        CoreSeed.Audit(Scopes, 7, ServiceAccount, "umbraco/automate/x/y", T0.AddMinutes(1), string.Concat(Enumerable.Repeat("{\"a\":", 17)) + "1" + new string('}', 17));

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items, Has.Count.EqualTo(1));
        Assert.That(page.Items[0].Actor.UserKey, Is.EqualTo(Sam));
    }

    [Test]
    public async Task Limit_TruncatesNewestFirst()
    {
        for (var i = 0; i < 5; i++)
        {
            SeedRun($"user:{Sam:D}", T0.AddMinutes(i));
        }

        ActivitySourcePage page = await Source().QueryAsync(Query(limit: 2), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.OccurredUtc), Is.EqualTo(new[] { new DateTimeOffset(T0.AddMinutes(4)), new DateTimeOffset(T0.AddMinutes(3)) }));
        Assert.That(page.Truncated, Is.True);
        Assert.That(page.Total, Is.EqualTo(5));
    }

    [Test]
    public async Task GetAsync_AutomateRow_WithDetails_CoreRowNull()
    {
        SeedCoreRows();
        SeedRun($"user:{Sam:D}", T0);
        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);
        string id = page.Items.Single().Id;

        ActivityEvent? found = await Source().GetAsync(id, CancellationToken.None);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Details!["runId"], Is.EqualTo("6f1c2a10-0000-4000-8000-000000000002"));
        Assert.That(found.IpAddress, Is.Null);
        Assert.That(await Source().GetAsync("audit:1", CancellationToken.None), Is.Null, "core row (user save)");
        Assert.That(await Source().GetAsync("log:1", CancellationToken.None), Is.Null);
        Assert.That(await Source().GetAsync("audit:x", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task ClaimedPrefix_SkippedByCore_WithTheRealSource()
    {
        SeedCoreRows();
        SeedRun($"user:{Sam:D}", T0);
        var options = new TestOptionsMonitor<UserActivityOptions>(new UserActivityOptions());
        var core = new CoreSource(Scopes, new CoreClaims(() => [Source()]), options);

        ActivitySourcePage page = await core.QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.RawType), Has.None.StartsWith("umbraco/automate/"));
        Assert.That(page.Items, Has.Count.EqualTo(2));
    }
}
