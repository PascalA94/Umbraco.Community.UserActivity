using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Tests.Sources;

namespace Umbraco.Community.UserActivity.Tests.Query;

public class ActivityQueryEngineTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly FakeTimeProvider _time = new(T0.AddDays(1));
    private readonly FakeLogger<ActivityQueryEngine> _logger = new();
    private readonly UserActivityOptions _options = new();

    public ActivityQueryEngineTests()
    {
        _users.GetUsersById(Arg.Any<int[]>()).Returns([]);
        _users.GetAsync(Arg.Any<Guid>()).Returns(Task.FromResult<IUser?>(null));
        _users.GetAsync(Arg.Any<IEnumerable<Guid>>()).Returns(Task.FromResult<IEnumerable<IUser>>([]));
    }

    private ActivityQueryEngine Engine(params IUserActivitySource[] sources)
        => new(
            new SourceAvailability(new UserActivitySourceCollection(() => sources), new StaticOptionsMonitor<UserActivityOptions>(_options)),
            new UserIdentityResolver(_users, new DictionaryAppCache()),
            new StaticOptionsMonitor<UserActivityOptions>(_options),
            _time,
            _logger,
            new SourceFailures());

    private static ActivityQuery Query(int skip = 0, int take = 50, Guid? userKey = null, string[]? sources = null, bool ascending = false, string? search = null)
        => new(userKey, new HashSet<string>(sources ?? []), new HashSet<string>(), new HashSet<string>(),
            new DateWindow(T0.AddDays(-30), T0.AddDays(1)), search, skip, take, ascending);

    private static ActivityEvent Event(string source, string id, DateTimeOffset at, ActivityActor? actor = null)
        => new(source, id, at, actor ?? new ActivityActor(ActorKind.User, null, 7, null, null), Categories.Saved, "Save", null, null, null, null);

    // A narrow time range forces ties, and ids of 1-3 digits put "x:9" and "x:10" at page edges (QA-7).
    private static List<ActivityEvent> Events(string source, int count, int seed)
    {
        var random = new Random(seed);
        return Enumerable.Range(1, count)
            .Select(i => Event(source, $"x:{i}", T0.AddSeconds(-random.Next(0, 8))))
            .ToList();
    }

    [Fact]
    public async Task Merge_ThreeSources_OrderedByTimeSourceId()
    {
        var a = new FakeSource("alpha", events: [Event("alpha", "a:1", T0), Event("alpha", "a:2", T0.AddMinutes(-2))]);
        var b = new FakeSource("beta", events: [Event("beta", "b:1", T0.AddMinutes(-1))]);
        var c = new FakeSource("gamma", events: [Event("gamma", "c:1", T0.AddMinutes(-3)), Event("gamma", "c:2", T0)]);

        ActivityPage page = await Engine(a, b, c).QueryAsync(Query(), CancellationToken.None);

        // Descending reverses the whole key, so on a time tie the later alias comes first.
        Assert.Equal(["c:2", "a:1", "b:1", "a:2", "c:1"], page.Items.Select(i => i.Id));
        Assert.Equal(5, page.Total);
        Assert.True(page.TotalIsExact);
        Assert.All(page.Sources, s => Assert.Equal(SourceStatus.Ok, s.Status));
    }

    [Fact]
    public async Task Merge_TiesStable()
    {
        var a = new FakeSource("alpha", events: [Event("alpha", "a:9", T0), Event("alpha", "a:10", T0)]);
        var b = new FakeSource("beta", events: [Event("beta", "b:1", T0)]);

        ActivityPage desc = await Engine(a, b).QueryAsync(Query(), CancellationToken.None);
        ActivityPage asc = await Engine(a, b).QueryAsync(Query(ascending: true), CancellationToken.None);

        Assert.Equal(["b:1", "a:10", "a:9"], desc.Items.Select(i => i.Id));
        Assert.Equal(["a:9", "a:10", "b:1"], asc.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Page2_EqualsSliceOfFullMerge()
    {
        for (var seed = 1; seed <= 25; seed++)
        {
            var random = new Random(seed);
            IUserActivitySource[] sources =
            [
                new FakeSource("alpha", events: Events("alpha", random.Next(0, 60), seed)),
                new FakeSource("beta", events: Events("beta", random.Next(0, 60), seed + 100)),
                new FakeSource("gamma", events: Events("gamma", random.Next(0, 60), seed + 200)),
            ];
            List<ActivityEvent> all = sources.Cast<FakeSource>().SelectMany(f => f.Events)
                .Order(ActivityEventOrder.Descending).ToList();
            var take = random.Next(1, 30);
            var skip = random.Next(0, 80);

            ActivityPage page = await Engine(sources).QueryAsync(Query(skip, take), CancellationToken.None);

            Assert.Equal(all.Skip(skip).Take(take).Select(e => e.Id + e.Source), page.Items.Select(e => e.Id + e.Source));
        }
    }

    [Fact]
    public async Task SourcesAskedForSkipPlusTake()
    {
        var a = new FakeSource("alpha");

        await Engine(a).QueryAsync(Query(skip: 100, take: 50), CancellationToken.None);

        Assert.Equal(150, Assert.Single(a.Queries).Limit);
    }

    [Fact]
    public async Task DepthCap_SetsFlag_AndStops()
    {
        _options.MaxDepth = 100;
        var a = new FakeSource("alpha", events: Events("alpha", 300, 1));

        ActivityPage beyond = await Engine(a).QueryAsync(Query(skip: 100, take: 50), CancellationToken.None);
        ActivityPage atEdge = await Engine(a).QueryAsync(Query(skip: 60, take: 50), CancellationToken.None);
        ActivityPage inside = await Engine(a).QueryAsync(Query(skip: 0, take: 50), CancellationToken.None);

        Assert.True(beyond.DepthCapped);
        Assert.Empty(beyond.Items);
        // QA-6: nothing ran, so no source may claim "ok".
        Assert.Empty(beyond.Sources);
        Assert.Single(a.Queries.Where(q => q.Limit > 0), q => q.Limit == 110);
        Assert.True(atEdge.DepthCapped);
        Assert.Equal(50, atEdge.Items.Count);
        Assert.False(inside.DepthCapped);
    }

    [Fact]
    public async Task Total_SumWhenAllCount_ElseInexact()
    {
        var counts = new FakeSource("alpha", events: Events("alpha", 3, 1));
        var countsToo = new FakeSource("beta", events: Events("beta", 4, 2));
        var cannot = new FakeSource("gamma", events: Events("gamma", 5, 3), canCount: false);

        ActivityPage exact = await Engine(counts, countsToo).QueryAsync(Query(), CancellationToken.None);
        ActivityPage inexact = await Engine(counts, countsToo, cannot).QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(7, exact.Total);
        Assert.True(exact.TotalIsExact);
        Assert.False(inexact.TotalIsExact);
        Assert.Equal(12, inexact.Total);
        Assert.Null(inexact.Sources.Single(s => s.Alias == "gamma").Count);
    }

    [Fact]
    public async Task InexactTotal_WithMoreRows_PromisesANextPage()
    {
        // A source that can't count (core during a search, uSync over its cap) must still let the pager reach page 2
        // (final review QA-F1): the lower bound goes past the rows returned when the source says more exist.
        var cannot = new FakeSource("gamma", events: Events("gamma", 120, 3), canCount: false);

        ActivityPage first = await Engine(cannot).QueryAsync(Query(skip: 0, take: 50), CancellationToken.None);
        ActivityPage last = await Engine(cannot).QueryAsync(Query(skip: 100, take: 50), CancellationToken.None);

        Assert.False(first.TotalIsExact);
        Assert.True(first.Total > 50);
        Assert.Equal(20, last.Items.Count);
        Assert.Equal(120, last.Total);
    }

    [Fact]
    public async Task SourceThrows_PartialResults_StatusFailed()
    {
        var ok = new FakeSource("alpha", events: Events("alpha", 2, 1));
        var broken = new FakeSource("beta") { OnQuery = (_, _) => throw new InvalidOperationException("sentinel-secret-name") };

        ActivityPage page = await Engine(broken, ok).QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(SourceStatus.Failed, page.Sources.Single(s => s.Alias == "beta").Status);
        Assert.False(page.TotalIsExact);
        FakeLogRecord warning = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("beta", warning.Message);
        Assert.Contains(nameof(InvalidOperationException), warning.Message);
        Assert.DoesNotContain("sentinel", warning.Message);
        Assert.Null(warning.Exception);
    }

    [Fact]
    public async Task SourceTimeout_PartialResults()
    {
        var never = new TaskCompletionSource();
        var slow = new FakeSource("alpha") { OnQuery = (_, _) => never.Task };
        var ok = new FakeSource("beta", events: Events("beta", 2, 1));

        Task<ActivityPage> running = Engine(slow, ok).QueryAsync(Query(), CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(_options.SourceTimeoutSeconds + 1));
        ActivityPage page = await running.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(SourceStatus.TimedOut, page.Sources.Single(s => s.Alias == "alpha").Status);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains("alpha", Assert.Single(_logger.Collector.GetSnapshot()).Message);
    }

    [Fact]
    public async Task SourceThrowsAfterTimeout_IsTimedOut()
    {
        // QA-2: a source whose own database timeout fires (it blocks, then throws) is reported as timed out.
        var slow = new FakeSource("alpha") { OnQuery = (_, _) => { _time.Advance(TimeSpan.FromSeconds(_options.SourceTimeoutSeconds)); throw new InvalidOperationException("db timeout"); } };

        ActivityPage page = await Engine(slow).QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(SourceStatus.TimedOut, Assert.Single(page.Sources).Status);
    }

    [Fact]
    public async Task Search_SkipsSourcesThatCannotSearch()
    {
        var searches = new FakeSource("alpha", events: Events("alpha", 2, 1));
        var cannot = new FakeSource("beta", events: Events("beta", 3, 2), canSearch: false);

        ActivityPage searched = await Engine(searches, cannot).QueryAsync(Query(search: "abc"), CancellationToken.None);
        ActivityPage plain = await Engine(searches, cannot).QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(2, searched.Items.Count);
        Assert.DoesNotContain(cannot.Queries, q => q.Search is not null);
        Assert.Equal(SourceStatus.Skipped, searched.Sources.Single(s => s.Alias == "beta").Status);
        Assert.Equal(5, plain.Items.Count);
    }

    [Fact]
    public async Task RequestCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        var a = new FakeSource("alpha") { OnQuery = (_, ct) => { cts.Cancel(); ct.ThrowIfCancellationRequested(); return Task.CompletedTask; } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Engine(a).QueryAsync(Query(), cts.Token));
    }

    [Fact]
    public async Task SourcesRunSequentially()
    {
        var running = 0;
        var maxRunning = 0;
        async Task Track(ActivitySourceQuery q, CancellationToken ct)
        {
            maxRunning = Math.Max(maxRunning, Interlocked.Increment(ref running));
            await Task.Yield();
            Interlocked.Decrement(ref running);
        }

        IUserActivitySource[] sources = [new FakeSource("alpha") { OnQuery = Track }, new FakeSource("beta") { OnQuery = Track }, new FakeSource("gamma") { OnQuery = Track }];

        await Engine(sources).QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task DuplicateIdInSource_Dropped()
    {
        var a = new FakeSource("alpha", events: [Event("alpha", "a:1", T0), Event("alpha", "a:1", T0), Event("alpha", "a:2", T0.AddMinutes(-1))]);
        var b = new FakeSource("beta", events: [Event("beta", "a:1", T0)]);

        ActivityPage page = await Engine(a, b).QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(["beta/a:1", "alpha/a:1", "alpha/a:2"], page.Items.Select(i => $"{i.Source}/{i.Id}"));
    }

    [Fact]
    public async Task ViaMatchesUserFilter()
    {
        var serviceKey = Guid.NewGuid();
        var samKey = Guid.NewGuid();
        IUser service = User(42, serviceKey);
        _users.GetAsync(serviceKey).Returns(service);
        var via = new ActivityActor(ActorKind.ServiceAccount, serviceKey, 42, null, null);
        var a = new FakeSource("alpha", events:
        [
            Event("alpha", "run", T0, new ActivityActor(ActorKind.User, samKey, 5, null, via)),
            Event("alpha", "own", T0.AddMinutes(-1), new ActivityActor(ActorKind.ServiceAccount, null, 42, null, null)),
            // A source that ignores the user filter must not leak someone else's row onto this tab.
            Event("alpha", "other", T0.AddMinutes(-2), new ActivityActor(ActorKind.User, Guid.NewGuid(), 6, null, null)),
        ]);

        ActivityPage page = await Engine(a).QueryAsync(Query(userKey: serviceKey), CancellationToken.None);

        Assert.Equal(["run", "own"], page.Items.Select(i => i.Id));
        Assert.Equal(serviceKey, Assert.Single(a.Queries).User!.Key);
        Assert.Equal(42, a.Queries[0].User!.Id);
    }

    [Fact]
    public async Task UnknownUserKey_NotFound_NoSourceQueried()
    {
        var a = new FakeSource("alpha", events: Events("alpha", 3, 1));

        ActivityPage page = await Engine(a).QueryAsync(Query(userKey: Guid.NewGuid()), CancellationToken.None);

        Assert.True(page.UserNotFound);
        Assert.Empty(page.Items);
        Assert.Empty(a.Queries);
    }

    [Fact]
    public async Task OnlyActiveSourcesQueried()
    {
        _options.Sources["beta"] = new SourceOptions { Enabled = false };
        var a = new FakeSource("alpha");
        var b = new FakeSource("beta");
        var c = new FakeSource("gamma") { IsAvailable = false };
        var d = new FakeSource("delta");

        ActivityPage all = await Engine(a, b, c, d).QueryAsync(Query(), CancellationToken.None);
        ActivityPage chosen = await Engine(a, b, c, d).QueryAsync(Query(sources: ["delta"]), CancellationToken.None);

        Assert.Equal(["alpha", "delta"], all.Sources.Select(s => s.Alias));
        Assert.Single(a.Queries);
        Assert.Empty(b.Queries);
        Assert.Empty(c.Queries);
        Assert.Equal(["delta"], chosen.Sources.Select(s => s.Alias));
        Assert.Equal(2, d.Queries.Count);
    }

    [Fact]
    public async Task Actors_ResolvedOncePerPage()
    {
        var a = new FakeSource("alpha", events: Events("alpha", 20, 1));

        ActivityPage page = await Engine(a).QueryAsync(Query(), CancellationToken.None);

        _users.Received(1).GetUsersById(Arg.Is<int[]>(ids => ids.SequenceEqual(new[] { 7 })));
        Assert.Equal(ActorKind.Unknown, page.Actors[ActorRef.ForId(7)].Kind);
    }

    [Fact]
    public async Task GetAsync_InactiveSource_Null_AndFailureIsNull()
    {
        _options.Sources["beta"] = new SourceOptions { Enabled = false };
        var a = new FakeSource("alpha", events: [Event("alpha", "a:1", T0)]);
        var b = new FakeSource("beta", events: [Event("beta", "b:1", T0)]);
        var broken = new BrokenGet("gamma");

        ActivityQueryEngine engine = Engine(a, b, broken);

        Assert.NotNull((await engine.GetAsync("alpha", "a:1", CancellationToken.None))?.Event);
        Assert.Null(await engine.GetAsync("beta", "b:1", CancellationToken.None));
        Assert.Null(await engine.GetAsync("nope", "x", CancellationToken.None));
        Assert.Null(await engine.GetAsync("gamma", "x", CancellationToken.None));
        Assert.Contains("gamma", Assert.Single(_logger.Collector.GetSnapshot()).Message);
    }

    private static IUser User(int id, Guid key)
    {
        IUser user = Substitute.For<IUser>();
        user.Id.Returns(id);
        user.Key.Returns(key);
        user.Username.Returns("service@example.test");
        return user;
    }

    private sealed class BrokenGet(string alias) : FakeSource(alias)
    {
        public override Task<ActivityEvent?> GetAsync(string id, CancellationToken ct) => throw new InvalidOperationException();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, "203.0.113.7")]
    public async Task ThirdPartyIp_OnlyWhenShowIpAddressesIsOn(bool show, string? expected)
    {
        // M3 review QA-3: the setting is enforced by the engine, not trusted to each source.
        _options.ShowIpAddresses = show;
        var withIp = Event("thirdparty", "t:1", T0) with { IpAddress = "203.0.113.7" };
        var source = new FakeSource("thirdparty", events: [withIp]);
        ActivityQueryEngine engine = Engine(source);

        ActivityPage page = await engine.QueryAsync(Query(), CancellationToken.None);
        ActivityDetail? detail = await engine.GetAsync("thirdparty", "t:1", CancellationToken.None);

        Assert.Equal(expected, Assert.Single(page.Items).IpAddress);
        Assert.Equal(expected, detail!.Event.IpAddress);
    }
}
