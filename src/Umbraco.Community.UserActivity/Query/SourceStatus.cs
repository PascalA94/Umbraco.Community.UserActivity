namespace Umbraco.Community.UserActivity.Query;

/// <summary>How one source did for a request (design 3.7, 5).</summary>
/// <param name="Count">The source's total for the filters, or null when it can't count or didn't answer.</param>
public sealed record SourceResult(string Alias, string Status, int? Count);

/// <summary>Permanent API values for <see cref="SourceResult.Status"/>.</summary>
public static class SourceStatus
{
    public const string Ok = "ok";
    public const string Failed = "failed";
    public const string TimedOut = "timedOut";

    public const string Skipped = "skipped";
}
