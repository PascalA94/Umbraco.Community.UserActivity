using System.Text.RegularExpressions;
using NPoco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.IntegrationTests.Core;

[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class CoreAuditSourceTests : UmbracoIntegrationTest
{
    private static readonly DateTime T0 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private readonly List<IClaimsCoreAuditRows> _claims = [];

    private IScopeProvider Scopes => GetRequiredService<IScopeProvider>();

    private TestUsers TestUsers => new(Services);

    [SetUp]
    public void ClearClaims() => _claims.Clear();

    private CoreSource Source(bool showIp = false)
        => new(Scopes, new CoreClaims(() => _claims), new TestOptionsMonitor<UserActivityOptions>(new UserActivityOptions { ShowIpAddresses = showIp }));

    private static ActivitySourceQuery Query(
        ResolvedUser? user = null, DateTime? from = null, DateTime? to = null, string? search = null, int limit = 50,
        string[]? categories = null, string[]? entityTypes = null, bool ascending = false)
        => new(user, new HashSet<string>(categories ?? []), new HashSet<string>(entityTypes ?? []),
            new DateTimeOffset(from ?? T0.AddDays(-1)), new DateTimeOffset(to ?? T0.AddDays(1)), search, limit, ascending);

    private static ResolvedUser Resolved(IUser u) => new(u.Key, u.Id, u.Username, u.Email, null);

    private async Task<IReadOnlyList<ActivityEvent>> AuditItemsAsync(ActivitySourceQuery query)
        => (await Source().QueryAsync(query, CancellationToken.None)).Items.Where(i => i.Id.StartsWith("audit:", StringComparison.Ordinal)).ToList();

    [Test]
    public async Task RealRows_ThroughAuditEntryService_Map()
    {
        IUser admin = await TestUsers.CreateAdminAsync("Sam Admin");
        IUser wren = await TestUsers.CreateAsync("Wren Writer", ["writer"]);
        IAuditEntryService entries = GetRequiredService<IAuditEntryService>();
        DateTime now = DateTime.UtcNow;
        await entries.WriteAsync(Constants.Security.SuperUserKey, "sentinel-performer", "203.0.113.7", now, wren.Key, "sentinel-affected", "umbraco/user/sign-in/login", "login");
        await entries.WriteAsync(admin.Key, "admin details", "203.0.113.8", now, wren.Key, "Wren Writer <wren@example.test>", "umbraco/user/password/reset", "reset");
        await entries.WriteAsync(admin.Key, "admin details", "203.0.113.8", now, wren.Key, "Wren Writer <wren@example.test>", "umbraco/user/save", "save");

        IReadOnlyList<ActivityEvent> items = await AuditItemsAsync(Query(from: now.AddMinutes(-5), to: now.AddMinutes(5)));

        ActivityEvent login = items.Single(i => i.RawType == "umbraco/user/sign-in/login");
        Assert.That(login.Category, Is.EqualTo(Categories.SignedIn));
        Assert.That(login.Actor.UserKey, Is.EqualTo(wren.Key), "sign-in actor is the affected user (S5)");
        ActivityEvent reset = items.Single(i => i.RawType == "umbraco/user/password/reset");
        Assert.That(reset.Category, Is.EqualTo(Categories.PasswordReset));
        Assert.That(reset.Actor.UserKey, Is.EqualTo(admin.Key));
        Assert.That(reset.Entity!.Type, Is.EqualTo("user"));
        Assert.That(reset.Entity.Key, Is.EqualTo(wren.Key.ToString("D")));
        Assert.That(items.All(i => i.Summary is null && i.Details is null && i.IpAddress is null), Is.True);
    }

    [Test]
    public async Task ByUserKey_Performer()
    {
        IUser admin = await TestUsers.CreateAdminAsync("Sam Admin");
        IUser wren = await TestUsers.CreateAsync("Wren Writer", ["writer"]);
        CoreSeed.Audit(Scopes, admin.Id, admin.Key, "umbraco/user/save", T0, affectedUserId: wren.Id, affectedUserKey: wren.Key, affectedDetails: "Wren Writer <wren@example.test>");
        CoreSeed.Audit(Scopes, wren.Id, wren.Key, "umbraco/user/save", T0.AddMinutes(1), affectedUserId: wren.Id, affectedUserKey: wren.Key);
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/sign-in/login", T0.AddMinutes(2), affectedUserId: wren.Id, affectedUserKey: wren.Key);
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/sign-in/login", T0.AddMinutes(3), affectedUserId: admin.Id, affectedUserKey: admin.Key);

        IReadOnlyList<ActivityEvent> adminItems = await AuditItemsAsync(Query(Resolved(admin)));
        IReadOnlyList<ActivityEvent> wrenItems = await AuditItemsAsync(Query(Resolved(wren)));

        Assert.That(adminItems.Select(i => i.OccurredUtc.UtcDateTime), Is.EqualTo(new[] { T0.AddMinutes(3), T0 }));
        Assert.That(wrenItems.Select(i => i.OccurredUtc.UtcDateTime), Is.EqualTo(new[] { T0.AddMinutes(2), T0.AddMinutes(1) }));

        ActivityEvent detail = (await Source().GetAsync(adminItems[1].Id, CancellationToken.None))!;
        Assert.That(detail.Entity!.Key, Is.EqualTo(wren.Key.ToString("D")));
        Assert.That(detail.Details!["affectedDetails"], Is.EqualTo("Wren Writer <wren@example.test>"));
        Assert.That(detail.Details.ContainsKey("performingIp"), Is.False);
    }

    [Test]
    public async Task SystemAndFailedSignIn_Actors()
    {
        CoreSeed.Audit(Scopes, 0, null, "umbraco/user/save", T0, eventDetails: "updating FailedPasswordAttempts");
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/sign-in/failed", T0.AddMinutes(1), eventDetails: "typed-secret-username");

        IReadOnlyList<ActivityEvent> items = await AuditItemsAsync(Query());

        Assert.That(items.Single(i => i.RawType == "umbraco/user/save").Actor.Kind, Is.EqualTo(ActorKind.System));
        ActivityEvent failed = items.Single(i => i.RawType == "umbraco/user/sign-in/failed");
        Assert.That(failed.Actor.Kind, Is.EqualTo(ActorKind.Unknown), "a failed sign-in names nobody; -1 is not a real actor (S5)");
        Assert.That(failed.Actor.UserKey, Is.Null);
    }

    [Test]
    public async Task FailedSignIn_DetailsNotInSummary()
    {
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/sign-in/failed", T0, eventDetails: "typed-secret-username", performingDetails: "typed-secret-username");

        ActivityEvent listed = (await AuditItemsAsync(Query())).Single();
        ActivityEvent detail = (await Source().GetAsync(listed.Id, CancellationToken.None))!;

        Assert.That(listed.Summary, Is.Null);
        Assert.That(listed.ToString(), Does.Not.Contain("typed-secret"));
        Assert.That(detail.Summary, Is.Null);
        Assert.That(detail.Details!["eventDetails"], Is.EqualTo("typed-secret-username"), "admins see it in the detail only (design 7.2)");
    }

    [Test]
    public async Task IpAddress_NullByDefault()
    {
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0, performingIp: "203.0.113.77");

        ActivityEvent listed = (await AuditItemsAsync(Query())).Single();
        ActivityEvent detail = (await Source().GetAsync(listed.Id, CancellationToken.None))!;

        Assert.That(listed.IpAddress, Is.Null);
        Assert.That(detail.IpAddress, Is.Null);
        Assert.That(detail.Details!.Values, Has.None.Contains("203.0.113.77"));
    }

    [Test]
    public async Task IpAddress_ShownWhenSettingOn()
    {
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0, performingIp: "203.0.113.77");

        ActivityEvent listed = (await Source(showIp: true).QueryAsync(Query(), CancellationToken.None)).Items.Single();
        ActivityEvent detail = (await Source(showIp: true).GetAsync(listed.Id, CancellationToken.None))!;

        Assert.That(listed.IpAddress, Is.EqualTo("203.0.113.77"));
        Assert.That(detail.IpAddress, Is.EqualTo("203.0.113.77"));
    }

    [Test]
    public async Task ClaimedPrefix_SkippedByCore()
    {
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/automate/umbracoAutomate/createContent", T0, eventDetails: "{}");
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0.AddMinutes(1));

        IReadOnlyList<ActivityEvent> unclaimed = await AuditItemsAsync(Query());
        string automateId = unclaimed.Single(i => i.RawType.StartsWith("umbraco/automate/", StringComparison.Ordinal)).Id;
        _claims.Add(new Claims(["umbraco/automate/"], []));
        ActivitySourcePage claimed = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(unclaimed, Has.Count.EqualTo(2));
        Assert.That(claimed.Items.Select(i => i.RawType), Is.EqualTo(new[] { "umbraco/user/save" }));
        Assert.That(claimed.Total, Is.EqualTo(1));
        Assert.That(await Source().GetAsync(automateId, CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task ClaimedPrefix_WildcardsAreLiteral()
    {
        // A claim containing LIKE wildcards must not swallow other rows.
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0);
        _claims.Add(new Claims(["umbraco/_ser/"], []));

        Assert.That(await AuditItemsAsync(Query()), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DateWindow_BoundsById_AndIsInclusive()
    {
        for (var i = -3; i <= 13; i++)
        {
            CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0.AddHours(i), eventDetails: $"h{i}");
        }

        ActivitySourcePage page = await Source().QueryAsync(Query(from: T0, to: T0.AddHours(10), ascending: true), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.OccurredUtc.UtcDateTime), Is.EqualTo(Enumerable.Range(0, 11).Select(h => T0.AddHours(h))));
        Assert.That(page.Total, Is.EqualTo(11));
    }

    [TestCase(0, 16, Description = "whole table")]
    [TestCase(0, 0, Description = "first row only")]
    [TestCase(16, 16, Description = "last row only")]
    [TestCase(3, 9, Description = "inside, with gaps")]
    [TestCase(-5, 30, Description = "window wider than the table")]
    public async Task DateWindow_IdRange_MatchesPlainDateFilter_WithGaps(int fromHour, int toHour)
    {
        // QA-11: ids with gaps (alternate rows deleted, as after a purge) and windows on each edge.
        for (var i = 0; i <= 16; i++)
        {
            CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0.AddHours(i), eventDetails: $"h{i}");
        }

        using (IScope scope = Scopes.CreateScope())
        {
            // Cast: a bare string[] would bind as the params array itself (@0 = "h1" only).
            scope.Database.Execute("DELETE FROM umbracoAudit WHERE eventDetails IN (@0)", (object)new[] { "h1", "h2", "h5", "h6", "h7", "h11", "h12" });
            scope.Complete();
        }

        int[] kept = Enumerable.Range(0, 17).Except([1, 2, 5, 6, 7, 11, 12]).Where(h => h >= fromHour && h <= toHour).ToArray();

        ActivitySourcePage page = await Source().QueryAsync(Query(from: T0.AddHours(fromHour), to: T0.AddHours(toHour), ascending: true), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.OccurredUtc.UtcDateTime), Is.EqualTo(kept.Select(h => T0.AddHours(h))));
        Assert.That(page.Total, Is.EqualTo(kept.Length));
    }

    [Test]
    public async Task DateWindow_SingleRowTable()
    {
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0);

        Assert.That((await Source().QueryAsync(Query(from: T0, to: T0), CancellationToken.None)).Total, Is.EqualTo(1));
        Assert.That((await Source().QueryAsync(Query(from: T0.AddSeconds(1), to: T0.AddDays(1)), CancellationToken.None)).Total, Is.EqualTo(0));
    }

    [Test]
    public async Task DateWindow_Empty_WhenNoRowsInside()
    {
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0.AddDays(-10));
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0.AddDays(10));

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items, Is.Empty);
        Assert.That(page.Total, Is.EqualTo(0));
    }

    [Test]
    public async Task Filters_CategoryEntityTypeAndSearch()
    {
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/sign-in/login", T0, affectedUserId: -1, affectedUserKey: Constants.Security.SuperUserKey);
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/member/save", T0.AddMinutes(1), eventDetails: "member 100% saved");
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "custom/thing", T0.AddMinutes(2));

        Assert.That((await AuditItemsAsync(Query(categories: [Categories.SignedIn]))).Select(i => i.RawType), Is.EqualTo(new[] { "umbraco/user/sign-in/login" }));
        Assert.That((await AuditItemsAsync(Query(categories: [Categories.Other]))).Select(i => i.RawType), Is.EqualTo(new[] { "custom/thing" }));
        Assert.That((await AuditItemsAsync(Query(entityTypes: ["member"]))).Select(i => i.RawType), Is.EqualTo(new[] { "umbraco/member/save" }));
        Assert.That((await AuditItemsAsync(Query(search: "100%"))).Select(i => i.RawType), Is.EqualTo(new[] { "umbraco/member/save" }));
        Assert.That(await AuditItemsAsync(Query(categories: [Categories.Published])), Is.Empty);
    }

    [Test]
    public async Task CoreSource_MergesLogAndAudit_NewestFirst()
    {
        CoreSeed.Log(Scopes, -1, 1, "Document", "Save", "log row", T0.AddMinutes(1));
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0);
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/save", T0.AddMinutes(2));

        ActivitySourcePage page = await Source().QueryAsync(Query(limit: 2), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.OccurredUtc.UtcDateTime), Is.EqualTo(new[] { T0.AddMinutes(2), T0.AddMinutes(1) }));
        Assert.That(page.Total, Is.EqualTo(3));
        Assert.That(page.Truncated, Is.True);
    }

    [Test]
    public void CoreAuditSql_InjectionStrings_AreParameters()
    {
        ISqlContext ctx = Scopes.SqlContext;
        foreach (var value in new[] { "'; DROP TABLE umbracoAudit;--", "%", "_", "[" })
        {
            var filter = new CoreAuditSql.AuditFilter(1, 10, T0, T0, Guid.Empty, 7, [value], false, [value], [value], value, false);
            foreach (Sql<ISqlContext> sql in new[] { CoreAuditSql.Audit(ctx, filter, count: false), CoreAuditSql.Audit(ctx, filter, count: true) })
            {
                string text = Regex.Replace(sql.SQL, @"\[(\w+)\]", "$1");
                Assert.That(text, Does.Not.Contain(value), sql.SQL);
                Assert.That(Regex.IsMatch(sql.SQL, @"SELECT\s+\*|\.\*", RegexOptions.IgnoreCase), Is.False);
            }
        }
    }

    private sealed class Claims(string[] prefixes, string[] entityTypes) : IClaimsCoreAuditRows
    {
        public IReadOnlyCollection<string> ClaimedAuditEventTypePrefixes => prefixes;

        public IReadOnlyCollection<string> ClaimedLogEntityTypes => entityTypes;
    }
}
