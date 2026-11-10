using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.IntegrationTests.Recorder;

[TestFixture]
public class RecorderStoreTests : RecorderTestBase
{
    private RecorderSource Source(int retentionDays = 0)
    {
        var handlers = new RecorderHandlerRegistry();
        handlers.Register("csp");
        return new RecorderSource(Scopes, Options(retentionDays), handlers);
    }

    private static ActivitySourceQuery Query(ResolvedUser? user = null, string? search = null, string[]? categories = null, string[]? entityTypes = null, bool ascending = false, int limit = 50)
        => new(user, new HashSet<string>(categories ?? []), new HashSet<string>(entityTypes ?? []),
            DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddMinutes(5), search, limit, ascending);

    [SetUp]
    public async Task ApplyPlan() => await ApplyMigrationPlanAsync();

    [Test]
    public async Task WriterThenSource_RoundTrip_ListAndDetail()
    {
        var user = Guid.NewGuid();
        Writer().Write(Entry("Main policy", user));

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        ActivityEvent e = page.Items.Single();
        Assert.That(page.Total, Is.EqualTo(1));
        Assert.That(e.Source, Is.EqualTo("recorder"));
        Assert.That(e.Actor.UserKey, Is.EqualTo(user));
        Assert.That(e.Actor.StoredName, Is.EqualTo("Wren Writer"));
        Assert.That(e.Entity!.Name, Is.EqualTo("Main policy"));
        Assert.That(e.Details, Is.Null, "list rows carry no details");
        Assert.That(e.IpAddress, Is.Null);

        ActivityEvent detail = (await Source().GetAsync(e.Id, CancellationToken.None))!;
        Assert.That(detail.Details!["mode"], Is.EqualTo("report"));
        Assert.That(await Source().GetAsync("rec:999999", CancellationToken.None), Is.Null);
        Assert.That(await Source().GetAsync("log:1", CancellationToken.None), Is.Null);
        Assert.That(await Source().GetAsync("rec:1; DROP TABLE x", CancellationToken.None), Is.Null);
    }

    [Test]
    public void Write_InsideACallersScope_FollowsTheCallersOutcome_RolledBackWhenTheCallerDoesNotComplete()
    {
        // The recorder joins the other package's transaction (design 4.1), so an audit row never survives a save that
        // was rolled back: the trail must not claim a change the other package did not make.
        using (IScope caller = Scopes.CreateScope())
        {
            Writer().Write(Entry("Rolled back"));
            Assert.That(caller.Database.ExecuteScalar<int>($"SELECT COUNT(*) FROM {caller.SqlContext.SqlSyntax.GetQuotedTableName(RecorderTable.Name)}"), Is.EqualTo(1), "visible inside the caller's transaction");
        }

        Assert.That(Count(), Is.EqualTo(0));

        using (IScope caller = Scopes.CreateScope())
        {
            Writer().Write(Entry("Committed"));
            caller.Complete();
        }

        Assert.That(Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Source_FiltersByUserCategoryEntityTypeAndSearch_AndOrders()
    {
        var wren = Guid.NewGuid();
        var sam = Guid.NewGuid();
        var time = new FixedClock(DateTimeOffset.UtcNow.AddMinutes(-30));
        RecorderWriter writer = Writer(time);
        writer.Write(Entry("Alpha 100%", wren));
        time.Advance(TimeSpan.FromMinutes(1));
        writer.Write(Entry("Beta_one", sam, category: "deleted", rawType: "RedirectDeletedNotification", entityType: "redirect"));
        time.Advance(TimeSpan.FromMinutes(1));
        writer.Write(Entry("Gamma", null));

        Assert.That((await Source().QueryAsync(Query(), CancellationToken.None)).Items.Select(i => i.Entity!.Name), Is.EqualTo(new[] { "Gamma", "Beta_one", "Alpha 100%" }));
        Assert.That((await Source().QueryAsync(Query(ascending: true), CancellationToken.None)).Items.Select(i => i.Entity!.Name), Is.EqualTo(new[] { "Alpha 100%", "Beta_one", "Gamma" }));
        Assert.That((await Source().QueryAsync(Query(new ResolvedUser(sam, 1, "sam", null, null)), CancellationToken.None)).Items.Single().Entity!.Name, Is.EqualTo("Beta_one"));
        Assert.That((await Source().QueryAsync(Query(categories: ["deleted"]), CancellationToken.None)).Items.Single().Entity!.Name, Is.EqualTo("Beta_one"));
        Assert.That((await Source().QueryAsync(Query(entityTypes: ["redirect"]), CancellationToken.None)).Items, Has.Count.EqualTo(1));
        Assert.That((await Source().QueryAsync(Query(entityTypes: ["nothing"]), CancellationToken.None)).Items, Is.Empty);

        // LIKE wildcards in the search text match themselves only (design 3.8).
        Assert.That((await Source().QueryAsync(Query(search: "100%"), CancellationToken.None)).Items.Single().Entity!.Name, Is.EqualTo("Alpha 100%"));
        Assert.That((await Source().QueryAsync(Query(search: "Gamm_"), CancellationToken.None)).Items, Is.Empty);
        Assert.That((await Source().QueryAsync(Query(search: "ta_o"), CancellationToken.None)).Items.Single().Entity!.Name, Is.EqualTo("Beta_one"));
        Assert.That((await Source().QueryAsync(Query(search: "%"), CancellationToken.None)).Items.Single().Entity!.Name, Is.EqualTo("Alpha 100%"));
        Assert.That((await Source().QueryAsync(Query(search: "[a]"), CancellationToken.None)).Items, Is.Empty);
        Assert.That((await Source().QueryAsync(Query(search: "x' OR '1'='1"), CancellationToken.None)).Items, Is.Empty);

        ActivitySourcePage limited = await Source().QueryAsync(Query(limit: 2), CancellationToken.None);
        Assert.That(limited.Items, Has.Count.EqualTo(2));
        Assert.That(limited.Truncated, Is.True);
        Assert.That(limited.Total, Is.EqualTo(3));
    }

    [Test]
    public async Task Source_DateWindowIsRespected()
    {
        Writer(new FixedClock(DateTimeOffset.UtcNow.AddDays(-40))).Write(Entry("Old"));
        Writer().Write(Entry("New"));

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.That(page.Items.Select(i => i.Entity!.Name), Is.EqualTo(new[] { "New" }));
    }

    [Test]
    public void Writer_OverlongValues_AreCapped_AndStillStored()
    {
        // SQL Server rejects an over-wide value, so storing means the caps held (the writer would otherwise swallow
        // the failure and the row would be missing).
        var huge = new string('x', 10_000);
        var log = new ListLogger<RecorderWriter>();
        var writer = new RecorderWriter(Scopes, TimeProvider.System, Options(), log);

        writer.Write(new RecorderEntry(huge, "saved", huge, Guid.NewGuid(), huge, huge, huge, huge, new Dictionary<string, string> { ["a"] = huge, ["b"] = huge, ["c"] = "kept" }));

        Assert.That(log.Entries, Is.Empty);
        Assert.That(Count(), Is.EqualTo(1));
    }

    [Test]
    public void Writer_TableMissing_SwallowsAndLogsTypeOnly()
    {
        using (IScope scope = Scopes.CreateScope(autoComplete: true))
        {
            scope.Database.Execute($"DROP TABLE {RecorderTable.Name}");
        }

        var log = new ListLogger<RecorderWriter>();
        var writer = new RecorderWriter(Scopes, TimeProvider.System, Options(), log);

        Assert.DoesNotThrow(() => writer.Write(Entry("SentinelEntityName")));

        (Microsoft.Extensions.Logging.LogLevel level, string message, Exception? exception) = log.Entries.Single();
        Assert.That(level, Is.EqualTo(Microsoft.Extensions.Logging.LogLevel.Warning));
        Assert.That(exception, Is.Null);
        Assert.That(message, Does.Not.Contain("SentinelEntityName").And.Not.Contain(RecorderTable.Name));
    }

    [Test]
    public void Writer_FailureInsideACallersScope_LeavesTheCallersWorkCommitted()
    {
        // Another package's save is in a scope of its own while our handler runs (Skybrud's delete, S4). If our write
        // fails there, that save's own rows must still commit. A trigger makes the insert fail on purpose (SQLite only:
        // SQL Server's failure modes differ and release-checks covers the happy path there).
        using (IScope setup = Scopes.CreateScope(autoComplete: true))
        {
            if (!setup.SqlContext.DatabaseType.GetType().Name.Contains("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Ignore("SQLite trigger only.");
            }

            setup.Database.Execute(
                $"CREATE TRIGGER failBoom BEFORE INSERT ON {RecorderTable.Name} WHEN NEW.RawType = 'Boom' BEGIN SELECT RAISE(ABORT, 'boom'); END;");
        }

        using (IScope outer = Scopes.CreateScope())
        {
            Writer().Write(Entry("Kept by the caller", rawType: "Fine"));
            Writer().Write(Entry("Fails", rawType: "Boom"));
            outer.Complete();
        }

        Assert.That(Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Retention_Days_DeletesInBatchesOf1000()
    {
        var old = DateTime.UtcNow.AddDays(-40);
        Seed(2500, old);
        Seed(5, DateTime.UtcNow.AddDays(-1));
        var log = new ListLogger<RetentionJob>();
        var job = new RetentionJob(Scopes, Options(30), TimeProvider.System, log);

        await job.ExecuteAsync();

        Assert.That(Count(), Is.EqualTo(5));
        (Microsoft.Extensions.Logging.LogLevel level, string message, _) = log.Entries.Single();
        Assert.That(level, Is.EqualTo(Microsoft.Extensions.Logging.LogLevel.Information));
        Assert.That(message, Does.Contain("2500"));
    }

    [Test]
    public void Retention_Purge_CountsBatches()
    {
        Seed(2000, DateTime.UtcNow.AddDays(-40));
        Seed(1, DateTime.UtcNow);
        var job = new RetentionJob(Scopes, Options(30), TimeProvider.System, new ListLogger<RetentionJob>());

        PurgeResult result = job.Purge(DateTime.UtcNow.AddDays(-30), CancellationToken.None);

        Assert.That(result, Is.EqualTo(new PurgeResult(2000, 2)));
        Assert.That(Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Retention_DefaultZero_KeepsEverything()
    {
        Seed(10, DateTime.UtcNow.AddYears(-5));
        var log = new ListLogger<RetentionJob>();

        await new RetentionJob(Scopes, Options(0), TimeProvider.System, log).ExecuteAsync();

        Assert.That(Count(), Is.EqualTo(10));
        Assert.That(log.Entries, Is.Empty);
    }

    private void Seed(int count, DateTime occurredUtc)
    {
        using IScope scope = Scopes.CreateScope(autoComplete: true);
        var rows = Enumerable.Range(0, count).Select(_ => RecorderWriter.ToDto(Entry(), occurredUtc)).ToList();
        scope.Database.InsertBulk(rows);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public void Advance(TimeSpan by) => _now += by;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
