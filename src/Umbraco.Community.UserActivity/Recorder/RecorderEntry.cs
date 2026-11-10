namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>What a handler asks <see cref="RecorderWriter"/> to store. Over-long values are capped there.</summary>
/// <param name="Source">The recorded package: <c>csp</c>, <c>workflow</c>, <c>skybrudRedirects</c>, <c>urlTracker</c>.</param>
/// <param name="Category">One of the normalised categories; anything else is stored as <c>other</c>.</param>
/// <param name="RawType">The notification type name.</param>
/// <param name="UserName">The name at the time, kept so a later deleted user is still readable.</param>
/// <param name="Details">Allow-listed keys per handler, never request bodies, secrets or policy text.</param>
internal sealed record RecorderEntry(
    string Source,
    string Category,
    string RawType,
    Guid? UserKey,
    string? UserName,
    string EntityType,
    string? EntityKey,
    string? EntityName,
    IReadOnlyDictionary<string, string>? Details)
{
    /// <summary>Names, keys and details are personal data: a record's default ToString would print them.</summary>
    public override string ToString() => $"RecorderEntry {{ Source = {Source}, RawType = {RawType} }}";
}
