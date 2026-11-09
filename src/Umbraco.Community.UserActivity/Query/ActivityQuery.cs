namespace Umbraco.Community.UserActivity.Query;

/// <summary>A validated list request (design 5). Empty sets mean "all".</summary>
/// <param name="UserKey">Actor or <c>Via</c> filter; null for everyone.</param>
/// <param name="Sources">Source aliases to include; only active sources are ever queried.</param>
public sealed record ActivityQuery(
    Guid? UserKey,
    IReadOnlySet<string> Sources,
    IReadOnlySet<string> Categories,
    IReadOnlySet<string> EntityTypes,
    DateWindow Window,
    string? Search,
    int Skip,
    int Take,
    bool Ascending)
{
    public override string ToString() => $"ActivityQuery {{ Skip = {Skip}, Take = {Take}, Ascending = {Ascending} }}";
}
