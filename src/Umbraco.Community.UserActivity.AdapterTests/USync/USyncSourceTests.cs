using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using uSync.History;
using uSync.History.Service;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Community.UserActivity.Sources.Adapters.USync;

namespace Umbraco.Community.UserActivity.AdapterTests.USync;

/// <summary>
/// The <c>usync</c> adapter bound to a real uSync.History at <c>USyncRuntimeVersion</c>. uSync keeps one entry per
/// import or export run, with only the username of whoever ran it (design 3.5), and no paging.
/// </summary>
public class USyncSourceTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private readonly ISyncHistoryService _service = Substitute.For<ISyncHistoryService>();
    private List<HistoryInfo> _history = [];

    public USyncSourceTests()
    {
        _service.IsEnabled().Returns(true);
        _service.GetHistoryAsync().Returns(_ => Task.FromResult<IEnumerable<HistoryInfo>>(_history));
    }

    private USyncSource Source(ISyncHistoryService? service = null)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => service ?? _service);
        ServiceProvider provider = services.BuildServiceProvider();
        return new USyncSource(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<IServiceProviderIsService>());
    }

    private static HistoryInfo Entry(string? username, DateTime at, string method = "Import", int changes = 3, int total = 40, string file = "/srv/site/uSync/history/a.json")
    {
        var entry = new HistoryInfo();
        Set(entry, nameof(HistoryInfo.Username), username);
        Set(entry, nameof(HistoryInfo.Date), at);
        Set(entry, nameof(HistoryInfo.Method), method);
        Set(entry, nameof(HistoryInfo.Changes), changes);
        Set(entry, nameof(HistoryInfo.Total), total);
        Set(entry, nameof(HistoryInfo.FilePath), file);
        return entry;
    }

    private static void Set(HistoryInfo entry, string name, object? value)
        => typeof(HistoryInfo).GetProperty(name)!.GetSetMethod(nonPublic: true)!.Invoke(entry, [value]);

    private static ActivitySourceQuery Query(ResolvedUser? user = null, int limit = 50, bool ascending = false, string? search = null, string[]? categories = null, DateTime? from = null)
        => new(user, new HashSet<string>(categories ?? []), new HashSet<string>(),
            new DateTimeOffset(from ?? T0.AddDays(-1)), new DateTimeOffset(T0.AddDays(1)), search, limit, ascending);

    [Fact]
    public void RealAssembly_IsTheRuntimeVersion_AndThe_ProbePasses()
    {
        Assembly usync = typeof(ISyncHistoryService).Assembly;
        var expected = typeof(USyncSourceTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "USyncRuntimeVersion").Value!;

        SourceAvailabilityResult result = USyncGuard.Evaluate(usync);

        Assert.Equal(AdapterState.Registered, result.State);
        if (!expected.Contains('*'))
        {
            Assert.StartsWith(expected, usync.GetName().Version!.ToString());
        }
    }

    [Fact]
    public async Task Installed_ReturnsRows()
    {
        _history = [Entry("sam@example.test", T0, "Import", 3, 40), Entry("sam@example.test", T0.AddMinutes(-5), "Export", 0, 40, "/srv/b.json")];

        ActivitySourcePage page = await Source().QueryAsync(Query(), CancellationToken.None);

        Assert.Equal([Categories.Imported, Categories.Exported], page.Items.Select(i => i.Category));
        ActivityEvent e = page.Items[0];
        Assert.Equal(SourceAlias.USync, e.Source);
        Assert.StartsWith(USyncSource.IdPrefix, e.Id);
        Assert.Equal(new ActivityActor(ActorKind.User, null, null, "sam@example.test", null), e.Actor);
        Assert.Equal("Import", e.RawType);
        Assert.Equal("Import: 3 changes in 40 items", e.Summary);
        Assert.Null(e.Entity);
        Assert.Equal(2, page.Total);
        Assert.NotEqual(page.Items[0].Id, page.Items[1].Id);
    }

    [Fact]
    public async Task MatchesUsernameCaseInsensitive()
    {
        _history = [Entry("Sam@Example.TEST", T0), Entry("wren@example.test", T0.AddMinutes(-1), file: "/w.json")];
        var sam = new ResolvedUser(Guid.NewGuid(), 5, "sam@example.test", "other@example.test", null);

        ActivitySourcePage page = await Source().QueryAsync(Query(sam), CancellationToken.None);

        ActivityEvent e = Assert.Single(page.Items);
        Assert.True(ActivityQueryEngine.Involves(e.Actor, sam));
        Assert.Equal(1, page.Total);
    }

    [Fact]
    public async Task MatchesTheUsernameOnly_NeverTheEmail()
    {
        _history = [Entry("other@example.test", T0)];
        var sam = new ResolvedUser(Guid.NewGuid(), 5, "sam", "other@example.test", null);

        Assert.Empty((await Source().QueryAsync(Query(sam), CancellationToken.None)).Items);
    }

    [Fact]
    public async Task RenamedUser_ShowsStoredName()
    {
        // History keeps the username at the time; after a rename it no longer matches the user, and shows as stored.
        _history = [Entry("old-name", T0)];
        var renamed = new ResolvedUser(Guid.NewGuid(), 5, "new-name", null, null);

        Assert.Empty((await Source().QueryAsync(Query(renamed), CancellationToken.None)).Items);
        ActivityEvent e = Assert.Single((await Source().QueryAsync(Query(), CancellationToken.None)).Items);
        Assert.Equal("old-name", e.Actor.StoredName);
        Assert.Null(e.Actor.UserKey);
        Assert.Null(e.Actor.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task NoUsername_IsSystem(string? username)
    {
        _history = [Entry(username, T0)];

        ActivityEvent e = Assert.Single((await Source().QueryAsync(Query(), CancellationToken.None)).Items);

        Assert.Equal(ActorKind.System, e.Actor.Kind);
        Assert.Equal(USyncSource.BackgroundLabel, e.Actor.StoredName);
    }

    [Fact]
    public async Task InMemoryFilter_CappedAt5000Entries()
    {
        // History has no paging: only the newest 5,000 entries are read, and the total is then inexact.
        _history = Enumerable.Range(0, 5_010).Select(i => Entry("sam", T0.AddSeconds(-i), file: $"/{i}.json")).ToList();

        ActivitySourcePage page = await Source().QueryAsync(Query(limit: 6_000, from: T0.AddDays(-30)), CancellationToken.None);

        Assert.Equal(USyncSource.MaxEntries, page.Items.Count);
        Assert.Equal(T0.AddSeconds(-4_999), page.Items[^1].OccurredUtc.UtcDateTime);
        Assert.Null(page.Total);
        Assert.True(page.Truncated);
    }

    [Fact]
    public async Task Window_Limit_Ascending_Search_Categories()
    {
        _history =
        [
            Entry("sam", T0.AddDays(-3), file: "/old.json"),
            Entry("sam", T0, "Import", file: "/1.json"),
            Entry("sam", T0.AddMinutes(-1), "Export", file: "/2.json"),
            Entry("wren", T0.AddMinutes(-2), "Import", file: "/3.json"),
        ];

        ActivitySourcePage newest = await Source().QueryAsync(Query(limit: 2), CancellationToken.None);
        ActivitySourcePage oldest = await Source().QueryAsync(Query(limit: 1, ascending: true), CancellationToken.None);
        ActivitySourcePage exports = await Source().QueryAsync(Query(categories: [Categories.Exported]), CancellationToken.None);
        ActivitySourcePage none = await Source().QueryAsync(Query(categories: [Categories.Saved]), CancellationToken.None);
        ActivitySourcePage search = await Source().QueryAsync(Query(search: "WREN"), CancellationToken.None);

        Assert.Equal([T0, T0.AddMinutes(-1)], newest.Items.Select(i => i.OccurredUtc.UtcDateTime));
        Assert.Equal(3, newest.Total);
        Assert.True(newest.Truncated);
        Assert.Equal([T0.AddMinutes(-2)], oldest.Items.Select(i => i.OccurredUtc.UtcDateTime));
        Assert.Equal(["Export"], exports.Items.Select(i => i.RawType));
        Assert.Empty(none.Items);
        Assert.Equal(["wren"], search.Items.Select(i => i.Actor.StoredName));
    }

    [Fact]
    public async Task LocalTimes_AreConvertedToUtc()
    {
        DateTime local = DateTime.SpecifyKind(T0, DateTimeKind.Utc).ToLocalTime();
        _history = [Entry("sam", local)];

        ActivityEvent e = Assert.Single((await Source().QueryAsync(Query(), CancellationToken.None)).Items);

        Assert.Equal(new DateTimeOffset(T0), e.OccurredUtc);
    }

    [Fact]
    public async Task HistoryDisabled_Unavailable()
    {
        Assert.True(Source().IsAvailable);
        ISyncHistoryService disabled = Substitute.For<ISyncHistoryService>();
        disabled.IsEnabled().Returns(false);
        Assert.False(Source(disabled).IsAvailable);

        ServiceProvider empty = new ServiceCollection().BuildServiceProvider();
        Assert.False(new USyncSource(empty.GetRequiredService<IServiceScopeFactory>(), empty.GetRequiredService<IServiceProviderIsService>()).IsAvailable);
        await Task.CompletedTask;
    }

    [Fact]
    public void RetentionHint_MayBeCleared()
    {
        // uSync keeps history until someone clears it from its dashboard: no fixed retention to show.
        Assert.Null(Source().Info.RetentionHint);
        Assert.True(Source().Info.CanSearch);
        Assert.Equal([Categories.Imported, Categories.Exported, Categories.Other], Source().Info.Categories);
    }

    [Fact]
    public async Task Detail_ById_WithoutTheServerPath()
    {
        _history = [Entry("sam", T0, file: "/srv/site/uSync/history/secret-path.json")];
        string id = Assert.Single((await Source().QueryAsync(Query(), CancellationToken.None)).Items).Id;

        ActivityEvent? e = await Source().GetAsync(id, CancellationToken.None);

        Assert.NotNull(e);
        Assert.Equal("3", e!.Details!["changes"]);
        Assert.Equal("40", e.Details["total"]);
        Assert.DoesNotContain(e.Details.Values, v => v.Contains("secret-path", StringComparison.Ordinal));
        Assert.DoesNotContain("secret-path", id);
        Assert.Null(await Source().GetAsync("usync:nope", CancellationToken.None));
    }

    [Fact]
    public void IntegrationVersion_IsTheLoadedAssemblys()
        => Assert.Equal(typeof(ISyncHistoryService).Assembly.GetName().Version!.ToString(), Source().IntegrationVersion);

    [Theory]
    [InlineData(typeof(MissingMethodException))]
    [InlineData(typeof(InvalidOperationException))]
    public void IsEnabledThrows_HiddenNotThrown(Type exceptionType)
    {
        // M3 review QA-1: IsAvailable runs on every request; a changed or broken uSync must hide the source, not 500.
        ISyncHistoryService broken = Substitute.For<ISyncHistoryService>();
        broken.IsEnabled().Returns(_ => throw (Exception)Activator.CreateInstance(exceptionType)!);

        Assert.False(Source(broken).IsAvailable);
    }
}
