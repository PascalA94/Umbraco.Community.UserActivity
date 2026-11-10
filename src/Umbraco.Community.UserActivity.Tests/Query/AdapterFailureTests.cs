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
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Community.UserActivity.Tests.Sources;

namespace Umbraco.Community.UserActivity.Tests.Query;

/// <summary>
/// Design 3.2: an installed integration whose shape changed throws a binding exception at query time. That source is
/// failed until restart (one Warning: alias, exception type, integration version) and the rest of the view works.
/// </summary>
public class AdapterFailureTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly FakeLogger<ActivityQueryEngine> _logger = new();
    private readonly SourceFailures _failures = new();

    public AdapterFailureTests()
    {
        _users.GetUsersById(Arg.Any<int[]>()).Returns([]);
        _users.GetAsync(Arg.Any<IEnumerable<Guid>>()).Returns(Task.FromResult<IEnumerable<IUser>>([]));
    }

    private ActivityQueryEngine Engine(params IUserActivitySource[] sources)
    {
        var options = new StaticOptionsMonitor<UserActivityOptions>(new UserActivityOptions());
        return new(
            new SourceAvailability(new UserActivitySourceCollection(() => sources), options),
            new UserIdentityResolver(_users, new DictionaryAppCache()),
            options,
            new FakeTimeProvider(T0.AddDays(1)),
            _logger,
            _failures);
    }

    private static ActivityQuery Query()
        => new(null, new HashSet<string>(), new HashSet<string>(), new HashSet<string>(),
            new DateWindow(T0.AddDays(-30), T0.AddDays(1)), null, 0, 50, false);

    private static ActivityEvent Event(string source, string id)
        => new(source, id, T0, new ActivityActor(ActorKind.System, null, null, null, null), Categories.Ran, "x", null, null, null, null);

    public static TheoryData<Exception> BindingFailures => new()
    {
        new MissingMethodException("Umbraco.AI.Core.AuditLog.IAIAuditLogService", "GetAuditLogsPagedAsync"),
        new MissingFieldException("secret-field-name"),
        new TypeLoadException("secret type text"),
    };

    [Theory]
    [MemberData(nameof(BindingFailures))]
    public async Task MissingMethodAtQuery_SourceFailedOthersOk(Exception failure)
    {
        var broken = new ThrowingAdapter("ai", failure);
        var core = new FakeSource("core", events: [Event("core", "log:1")]);
        ActivityQueryEngine engine = Engine(core, broken);

        ActivityPage first = await engine.QueryAsync(Query(), CancellationToken.None);
        ActivityPage second = await engine.QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(["log:1"], first.Items.Select(i => i.Id));
        Assert.Equal(SourceStatus.Failed, first.Sources.Single(s => s.Alias == "ai").Status);
        Assert.Equal(SourceStatus.Ok, first.Sources.Single(s => s.Alias == "core").Status);
        Assert.Equal(SourceStatus.Failed, second.Sources.Single(s => s.Alias == "ai").Status);
        Assert.Equal(1, broken.Calls);

        FakeLogRecord warning = Assert.Single(_logger.Collector.GetSnapshot(), r => r.Level == LogLevel.Warning);
        Assert.Contains("ai", warning.Message);
        Assert.Contains(failure.GetType().FullName!, warning.Message);
        Assert.Contains("17.0.0.0", warning.Message);
        Assert.DoesNotContain("secret", warning.Message);
        Assert.DoesNotContain("GetAuditLogsPagedAsync", warning.Message);
        Assert.Null(warning.Exception);
    }

    [Fact]
    public async Task OrdinaryFailure_IsNotStuck()
    {
        var flaky = new ThrowingAdapter("ai", new InvalidOperationException("db down"));
        ActivityQueryEngine engine = Engine(flaky);

        await engine.QueryAsync(Query(), CancellationToken.None);
        await engine.QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(2, flaky.Calls);
    }

    [Fact]
    public async Task BrokenSource_Detail_IsNull_WithoutCallingIt()
    {
        var broken = new ThrowingAdapter("ai", new MissingMethodException("x", "y"));
        ActivityQueryEngine engine = Engine(broken);
        await engine.QueryAsync(Query(), CancellationToken.None);

        Assert.Null(await engine.GetAsync("ai", "ai:1", CancellationToken.None));
        Assert.Equal(1, broken.Calls);
    }

    [Fact]
    public async Task BindingFailureInDetail_AlsoMarksTheSource()
    {
        var broken = new ThrowingAdapter("ai", new MissingMethodException("x", "y"));
        ActivityQueryEngine engine = Engine(broken);

        Assert.Null(await engine.GetAsync("ai", "ai:1", CancellationToken.None));
        ActivityPage page = await engine.QueryAsync(Query(), CancellationToken.None);

        Assert.Equal(SourceStatus.Failed, page.Sources.Single().Status);
        Assert.Equal(1, broken.Calls);
    }

    private sealed class ThrowingAdapter(string alias, Exception failure) : FakeSource(alias), IRuntimeAdapter
    {
        public int Calls { get; private set; }

        public string? IntegrationVersion => "17.0.0.0";

        public override Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
        {
            Calls++;
            throw failure;
        }

        public override Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
        {
            Calls++;
            throw failure;
        }
    }
}
