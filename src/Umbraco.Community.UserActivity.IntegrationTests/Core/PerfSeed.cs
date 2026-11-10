using System.Diagnostics;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.IntegrationTests.Core;

/// <summary>
/// Local, advisory performance check (plan Task 11 Step 4, design 10): 1M umbracoLog and 1M umbracoAudit rows on
/// SQL Server, then page 1 and page 100 of a 30-day window, for one user and for everyone. Explicit, so CI never
/// runs it. Run with Docker SQL Server:
///   Tests__Database__DatabaseType=SqlServer Tests__Database__SQLServerMasterConnectionString="..." \
///   dotnet test --filter "FullyQualifiedName~PerfSeed"
/// Results go to the test output and are recorded by hand in docs/spikes.md.
/// </summary>
[TestFixture]
[Explicit("Local SQL Server performance run; seeds 2M rows.")]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class PerfSeed : UmbracoIntegrationTest
{
    private const int Rows = 1_000_000;
    private const int Days = 90;

    [Test]
    public async Task Page1_And_Page100_Under2Seconds()
    {
        IScopeProvider scopes = GetRequiredService<IScopeProvider>();
        Assert.That(scopes.SqlContext.SqlSyntax.ProviderName, Does.Contain("SqlClient"), "Run against SQL Server (see the class comment).");

        var users = new List<IUser>();
        for (var i = 0; i < 20; i++)
        {
            users.Add(await new TestUsers(Services).CreateAsync($"Perf User {i}", ["writer"]));
        }

        DateTime now = DateTime.UtcNow;
        Seed(scopes, users, now);

        var source = new CoreSource(scopes, new CoreClaims(() => []), new TestOptionsMonitor<UserActivityOptions>(new UserActivityOptions()));
        IUser target = users[3];
        var subject = new ResolvedUser(target.Key, target.Id, target.Username, target.Email, null);

        foreach ((string label, ResolvedUser? user, int days, string? search) in new[]
                 {
                     ("one user", subject, 30, (string?)null), ("all users", (ResolvedUser?)null, 30, null),
                     // QA-2: the worst case, a 366-day search that matches nothing (a full LIKE scan, no count).
                     ("all users, 366-day search, no match", null, 366, "zzzz-no-match"),
                 })
        {
            foreach ((string page, int limit) in new[] { ("page 1", 50), ("page 100", 5000) })
            {
                var query = new ActivitySourceQuery(user, new HashSet<string>(), new HashSet<string>(), now.AddDays(-days), now, search, limit, false);
                await source.QueryAsync(query, CancellationToken.None); // warm-up (plan cache)
                var watch = Stopwatch.StartNew();
                ActivitySourcePage result = await source.QueryAsync(query, CancellationToken.None);
                watch.Stop();
                TestContext.Out.WriteLine($"PERF {label}, {page}: {watch.ElapsedMilliseconds} ms, {result.Items.Count} items, total {result.Total}");
                Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)), $"{label}, {page}");
            }
        }
    }

    private static void Seed(IScopeProvider scopes, List<IUser> users, DateTime now)
    {
        // Rows spread evenly over 90 days, ids growing with time as on a real site. T-SQL: test-only, SQL Server only.
        using IScope scope = scopes.CreateScope();
        scope.Database.CommandTimeout = 600;
        scope.Database.Execute($@"
WITH n AS (SELECT TOP ({Rows}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c),
u AS (SELECT v.id, ROW_NUMBER() OVER (ORDER BY v.id) - 1 AS k FROM (VALUES {string.Join(",", users.Select(x => $"({x.Id})"))}) v(id))
INSERT INTO umbracoLog (userId, NodeId, entityType, Datestamp, logHeader, logComment, parameters)
SELECT u.id, 1000 + n.i % 5000, 'Document', DATEADD(SECOND, -CAST(({Days} * 86400.0) * ({Rows} - n.i) / {Rows} AS int), @0), CASE n.i % 3 WHEN 0 THEN 'Save' WHEN 1 THEN 'Publish' ELSE 'Move' END, 'perf row', NULL
FROM n JOIN u ON u.k = n.i % {users.Count}", now);
        scope.Database.Execute($@"
WITH n AS (SELECT TOP ({Rows}) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i FROM sys.all_objects a CROSS JOIN sys.all_objects b CROSS JOIN sys.all_objects c),
u AS (SELECT v.id, v.k2, ROW_NUMBER() OVER (ORDER BY v.id) - 1 AS k FROM (VALUES {string.Join(",", users.Select(x => $"({x.Id}, '{x.Key}')"))}) v(id, k2))
INSERT INTO umbracoAudit (performingUserId, performingUserKey, performingDetails, performingIp, eventDateUtc, affectedUserId, affectedUserKey, affectedDetails, eventType, eventDetails)
SELECT u.id, u.k2, 'perf', '203.0.113.1', DATEADD(SECOND, -CAST(({Days} * 86400.0) * ({Rows} - n.i) / {Rows} AS int), @0), 0, NULL, NULL, 'umbraco/user/save', 'perf'
FROM n JOIN u ON u.k = n.i % {users.Count}", now);
        scope.Complete();
    }
}
