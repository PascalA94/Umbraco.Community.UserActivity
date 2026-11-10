using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.AI.Core.AuditLog;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Community.UserActivity.Sources.Adapters.AI;

namespace Umbraco.Community.UserActivity.AdapterTests.AI;

/// <summary>
/// The <c>ai</c> adapter bound to a real Umbraco.AI.Core at <c>AIRuntimeVersion</c> (the line's oldest release by
/// default, the newest on the CI latest leg). The service is a substitute of the real interface, so these tests
/// exercise the real types and signatures the adapter was compiled against.
/// </summary>
public class AISourceTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Sam = Guid.Parse("1f0c0000-0000-4000-8000-0000000000aa");

    private readonly IAIAuditLogService _service = Substitute.For<IAIAuditLogService>();
    private readonly List<(AIAuditLogFilter Filter, int Skip, int Take)> _calls = [];
    private IReadOnlyList<AIAuditLog> _logs = [];
    private int _total;

    public AISourceTests()
        => _service.GetAuditLogsPagedAsync(Arg.Any<AIAuditLogFilter>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                _calls.Add((call.ArgAt<AIAuditLogFilter>(0), call.ArgAt<int>(1), call.ArgAt<int>(2)));
                return Task.FromResult<(IEnumerable<AIAuditLog>, int)>((_logs.Skip(call.ArgAt<int>(1)).Take(call.ArgAt<int>(2)).ToList(), _total));
            });

    private AISource Source(int? retentionDays = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _service);
        if (retentionDays is { } days)
        {
            services.Configure<AIAuditLogOptions>(o => o.RetentionDays = days);
        }

        services.AddOptions();
        ServiceProvider provider = services.BuildServiceProvider();
        return new AISource(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IServiceProviderIsService>(),
            provider.GetRequiredService<IOptionsMonitor<AIAuditLogOptions>>());
    }

    private static AIAuditLog Log(string? userId, DateTime start, string? userName = "Sam Admin", string? entityType = "document", string? entityId = null)
    {
        var log = new AIAuditLog();
        Set(log, nameof(AIAuditLog.Id), Guid.NewGuid());
        Set(log, nameof(AIAuditLog.StartTime), start);
        Set(log, nameof(AIAuditLog.UserId), userId);
        Set(log, nameof(AIAuditLog.UserName), userName);
        Set(log, nameof(AIAuditLog.EntityType), entityType);
        Set(log, nameof(AIAuditLog.EntityId), entityId);
        Set(log, nameof(AIAuditLog.FeatureType), "prompt");
        Set(log, nameof(AIAuditLog.ProviderId), "openai");
        Set(log, nameof(AIAuditLog.Status), AIAuditLogStatus.Succeeded);
        return log;
    }

    // Some members have non-public setters on some releases; the adapter only reads them.
    private static void Set(AIAuditLog log, string name, object? value)
        => typeof(AIAuditLog).GetProperty(name)!.GetSetMethod(nonPublic: true)!.Invoke(log, [value]);

    private static ActivitySourceQuery Query(ResolvedUser? user = null, int limit = 50, bool ascending = false, string? search = null, string[]? categories = null, string[]? entityTypes = null)
        => new(user, new HashSet<string>(categories ?? []), new HashSet<string>(entityTypes ?? []),
            new DateTimeOffset(T0.AddDays(-1)), new DateTimeOffset(T0.AddDays(1)), search, limit, ascending);

    [Fact]
    public void RealAssembly_IsTheRuntimeVersion_AndThe_ProbePasses()
    {
        Assembly ai = typeof(IAIAuditLogService).Assembly;
        var expected = typeof(AISourceTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "AIRuntimeVersion").Value!;

        SourceAvailabilityResult result = AIGuard.Evaluate(ai);

        Assert.Equal(AdapterState.Registered, result.State);
        Assert.Null(result.MissingMember);
        if (!expected.Contains('*'))
        {
            Assert.StartsWith(expected, ai.GetName().Version!.ToString());
        }
    }

    [Fact]
    public async Task Installed_ReturnsRows()
    {
        _logs = [Log(Sam.ToString("D"), T0, entityId: "c0000000-0000-4000-8000-000000000001")];
        _total = 1;

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        ActivityEvent e = Assert.Single(page.Items);
        Assert.Equal(SourceAlias.AI, e.Source);
        Assert.StartsWith(AISource.IdPrefix, e.Id);
        Assert.Equal(new DateTimeOffset(T0), e.OccurredUtc);
        Assert.Equal(new ActivityActor(ActorKind.User, Sam, null, "Sam Admin", null), e.Actor);
        Assert.Equal(Categories.Ran, e.Category);
        Assert.Equal("prompt", e.RawType);
        Assert.Equal(new ActivityEntity("document", "c0000000-0000-4000-8000-000000000001", "c0000000-0000-4000-8000-000000000001", null, true), e.Entity);
        Assert.Equal("prompt (Succeeded)", e.Summary);
        Assert.Null(e.IpAddress);
        Assert.Null(e.Details);
        Assert.Equal(1, page.Total);
        Assert.False(page.Truncated);
    }

    [Fact]
    public async Task FiltersByKeyString()
    {
        await Source().QueryAsync(Query(new ResolvedUser(Sam, 5, "sam", null, null), search: "abc"), CancellationToken.None);

        AIAuditLogFilter filter = Assert.Single(_calls).Filter;
        Assert.Equal(Sam.ToString("D"), filter.UserId);
        Assert.Equal(T0.AddDays(-1), filter.FromDate);
        Assert.Equal(DateTimeKind.Utc, filter.FromDate!.Value.Kind);
        Assert.Equal(T0.AddDays(1), filter.ToDate);
        Assert.Equal("abc", filter.SearchText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task NullUser_IsBackgroundSystem(string? userId)
    {
        _logs = [Log(userId, T0, userName: null)];
        _total = 1;

        ActivityEvent e = Assert.Single((await Source().QueryAsync(Query(), CancellationToken.None)).Items);

        Assert.Equal(ActorKind.System, e.Actor.Kind);
        Assert.Equal(AISource.BackgroundLabel, e.Actor.StoredName);
    }

    [Fact]
    public async Task NonKeyUser_IsUnknown_NeverMatchedByName()
    {
        _logs = [Log("sam", T0, userName: "sam")];
        _total = 1;

        ActivityEvent e = Assert.Single((await Source().QueryAsync(Query(), CancellationToken.None)).Items);

        Assert.Equal(new ActivityActor(ActorKind.Unknown, null, null, null, null), e.Actor);
    }

    [Fact]
    public async Task PagesWithSkipTake_UsesTotal()
    {
        _logs = Enumerable.Range(0, 5).Select(i => Log(Sam.ToString("D"), T0.AddMinutes(-i))).ToList();
        _total = 5;

        ActivitySourcePage page = await Source().QueryAsync(Query(limit: 2), CancellationToken.None);

        Assert.Equal((0, 2), (_calls.Single().Skip, _calls.Single().Take));
        Assert.Equal([T0, T0.AddMinutes(-1)], page.Items.Select(i => i.OccurredUtc.UtcDateTime));
        Assert.Equal(5, page.Total);
        Assert.True(page.Truncated);
    }

    [Fact]
    public async Task Ascending_ReadsTheLastPage_OldestFirst()
    {
        _logs = Enumerable.Range(0, 5).Select(i => Log(Sam.ToString("D"), T0.AddMinutes(-i))).ToList();
        _total = 5;

        ActivitySourcePage page = await Source().QueryAsync(Query(limit: 2, ascending: true), CancellationToken.None);

        Assert.Equal([0, 3], _calls.Select(c => c.Skip));
        Assert.Equal([T0.AddMinutes(-4), T0.AddMinutes(-3)], page.Items.Select(i => i.OccurredUtc.UtcDateTime));
        Assert.Equal(5, page.Total);
    }

    [Fact]
    public async Task OtherCategoryOrAnyEntityFilter_AsksNothing()
    {
        ActivitySourcePage saved = await Source().QueryAsync(Query(categories: [Categories.Saved]), CancellationToken.None);
        ActivitySourcePage document = await Source().QueryAsync(Query(entityTypes: ["document"]), CancellationToken.None);

        Assert.Empty(saved.Items);
        Assert.Empty(document.Items);
        Assert.Empty(_calls);
    }

    [Theory]
    [InlineData(null, 14)]
    [InlineData(30, 30)]
    [InlineData(0, null)]
    public void RetentionHint_14Days_OrFromOptions(int? configured, int? expectedDays)
        => Assert.Equal(expectedDays is { } d ? TimeSpan.FromDays(d) : null, Source(configured).Info.RetentionHint);

    [Fact]
    public async Task Detail_HasNoIp_NoPromptOrResponse()
    {
        AIAuditLog log = Log(Sam.ToString("D"), T0);
        _service.GetAuditLogAsync(log.Id, Arg.Any<CancellationToken>()).Returns(Task.FromResult<AIAuditLog?>(log));

        ActivityEvent? e = await Source().GetAsync(AISource.IdPrefix + log.Id.ToString("D"), CancellationToken.None);

        Assert.NotNull(e);
        Assert.Equal("Succeeded", e!.Details!["status"]);
        Regex ip = new(@"\b\d{1,3}(\.\d{1,3}){3}\b");
        Assert.DoesNotContain(e.Details.Values, v => ip.IsMatch(v));
        Assert.DoesNotContain(e.Details.Keys, k => k.Contains("prompt", StringComparison.OrdinalIgnoreCase) || k.Contains("response", StringComparison.OrdinalIgnoreCase));
        Assert.Null(await Source().GetAsync("ai:not-a-guid", CancellationToken.None));
        Assert.Null(await Source().GetAsync("log:1", CancellationToken.None));
    }

    [Fact]
    public void Available_OnlyWhenTheServiceIsRegistered()
    {
        Assert.True(Source().IsAvailable);
        ServiceProvider empty = new ServiceCollection().AddOptions().BuildServiceProvider();
        var hidden = new AISource(empty.GetRequiredService<IServiceScopeFactory>(), empty.GetRequiredService<IServiceProviderIsService>(), empty.GetRequiredService<IOptionsMonitor<AIAuditLogOptions>>());

        Assert.False(hidden.IsAvailable);
    }

    [Fact]
    public void IntegrationVersion_IsTheLoadedAssemblys()
        => Assert.Equal(typeof(IAIAuditLogService).Assembly.GetName().Version!.ToString(), Source().IntegrationVersion);
}
