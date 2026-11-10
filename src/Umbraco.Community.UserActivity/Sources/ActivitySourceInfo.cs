namespace Umbraco.Community.UserActivity.Sources;

/// <param name="EntityTypes">Entity types the source can return, for the filter list.</param>
/// <param name="Categories">Categories the source can return.</param>
/// <param name="CanCount">The source returns an exact <see cref="ActivitySourcePage.Total"/>.</param>
/// <param name="CanSearch">The source applies <see cref="ActivitySourceQuery.Search"/>.</param>
/// <param name="RetentionHint">How long the underlying store keeps rows, when known; null = kept by its owner.</param>
public sealed record ActivitySourceInfo(
    IReadOnlyCollection<string> EntityTypes,
    IReadOnlyCollection<string> Categories,
    bool CanCount,
    bool CanSearch,
    TimeSpan? RetentionHint);
