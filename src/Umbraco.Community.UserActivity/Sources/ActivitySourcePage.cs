namespace Umbraco.Community.UserActivity.Sources;

/// <param name="Total">Rows matching the filters, or null when the source can't count cheaply.</param>
/// <param name="Truncated">More rows exist than were returned.</param>
public sealed record ActivitySourcePage(IReadOnlyList<ActivityEvent> Items, int? Total, bool Truncated);
