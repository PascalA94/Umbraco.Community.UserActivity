namespace Umbraco.Community.UserActivity.Sources;

/// <summary>
/// The one order every merge uses (design 3.7): <c>OccurredUtc</c>, then source alias, then id. Ids compare by
/// length first, so ids sharing a prefix ("log:9", "log:10") sort numerically, matching a source's own
/// <c>ORDER BY date, id</c>. A source that returns rows in this order pages without gaps or repeats.
/// </summary>
public sealed class ActivityEventOrder : IComparer<ActivityEvent>
{
    public static ActivityEventOrder Ascending { get; } = new(descending: false);

    public static ActivityEventOrder Descending { get; } = new(descending: true);

    private readonly bool _descending;

    private ActivityEventOrder(bool descending) => _descending = descending;

    public int Compare(ActivityEvent? x, ActivityEvent? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null || y is null)
        {
            return x is null ? -1 : 1;
        }

        var result = x.OccurredUtc.UtcTicks.CompareTo(y.OccurredUtc.UtcTicks);
        if (result == 0)
        {
            result = string.CompareOrdinal(x.Source, y.Source);
        }

        if (result == 0)
        {
            result = x.Id.Length.CompareTo(y.Id.Length);
        }

        if (result == 0)
        {
            result = string.CompareOrdinal(x.Id, y.Id);
        }

        return _descending ? -result : result;
    }
}
