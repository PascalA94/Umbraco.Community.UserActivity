using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Tests.Sources;

/// <summary>A configurable in-memory source for unit tests.</summary>
public class FakeSource : IUserActivitySource, IClaimsCoreAuditRows
{
    public FakeSource(
        string alias,
        IReadOnlyCollection<string>? auditPrefixes = null,
        IReadOnlyCollection<string>? logEntityTypes = null,
        IReadOnlyList<ActivityEvent>? events = null,
        bool canCount = true,
        bool canSearch = true)
    {
        Alias = alias;
        ClaimedAuditEventTypePrefixes = auditPrefixes ?? [];
        ClaimedLogEntityTypes = logEntityTypes ?? [];
        Events = events ?? [];
        Info = new ActivitySourceInfo([], [], canCount, canSearch, RetentionHint: null);
    }

    public string Alias { get; }

    public string DisplayNameKey => "userActivity_source" + Alias;

    public bool IsAvailable { get; set; } = true;

    public ActivitySourceInfo Info { get; set; }

    public IReadOnlyCollection<string> ClaimedAuditEventTypePrefixes { get; }

    public IReadOnlyCollection<string> ClaimedLogEntityTypes { get; }

    public IReadOnlyList<ActivityEvent> Events { get; set; }

    public List<ActivitySourceQuery> Queries { get; } = [];

    public Func<ActivitySourceQuery, CancellationToken, Task>? OnQuery { get; set; }

    public virtual async Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        Queries.Add(query);
        if (OnQuery is not null)
        {
            await OnQuery(query, ct);
        }

        // The order every source must return (the merge relies on it).
        IEnumerable<ActivityEvent> ordered = Events.Order(query.Ascending ? ActivityEventOrder.Ascending : ActivityEventOrder.Descending);
        List<ActivityEvent> items = ordered.Take(query.Limit).ToList();
        return new ActivitySourcePage(items, Info.CanCount ? Events.Count : null, Events.Count > query.Limit);
    }

    public virtual Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
        => Task.FromResult(Events.FirstOrDefault(e => e.Id == id));
}
