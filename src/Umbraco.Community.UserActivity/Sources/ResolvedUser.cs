namespace Umbraco.Community.UserActivity.Sources;

/// <summary>
/// The user filter, resolved once per request (design 3.5) so each source can match in its own terms: core
/// <c>umbracoLog</c> by <see cref="Id"/>, <c>umbracoAudit</c> by <see cref="Key"/>, uSync by <see cref="Username"/>.
/// </summary>
public sealed record ResolvedUser(Guid Key, int Id, string? Username, string? Email, IReadOnlyCollection<string>? PreviousUsernames)
{
    public override string ToString() => $"ResolvedUser {{ Id = {Id} }}";
}
