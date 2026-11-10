namespace Umbraco.Community.UserActivity.Sources;

/// <summary>What the engine asks a source for. Every value is already validated by the API.</summary>
/// <param name="User">Resolved once by the engine (design 3.5); null means all users.</param>
/// <param name="Categories">Empty means every category.</param>
/// <param name="EntityTypes">Empty means every entity type.</param>
/// <param name="Search">3-100 printable characters, or null.</param>
/// <param name="Limit">Top N rows, newest first unless <paramref name="Ascending"/>.</param>
public sealed record ActivitySourceQuery(
    ResolvedUser? User,
    IReadOnlySet<string> Categories,
    IReadOnlySet<string> EntityTypes,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string? Search,
    int Limit,
    bool Ascending)
{
    /// <summary>No user or search text: these strings reach logs and exception messages.</summary>
    public override string ToString() => $"ActivitySourceQuery {{ FromUtc = {FromUtc:O}, ToUtc = {ToUtc:O}, Limit = {Limit}, Ascending = {Ascending} }}";
}
