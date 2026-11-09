namespace Umbraco.Community.UserActivity.Query;

/// <summary>A required, bounded date window (design 3.7): every query is limited in time so no scan is unbounded.</summary>
public sealed record DateWindow(DateTimeOffset FromUtc, DateTimeOffset ToUtc)
{
    /// <summary>
    /// Fills in the defaults (to = now, from = to minus <paramref name="defaultDays"/>) and checks the range.
    /// Errors name the query parameter only.
    /// </summary>
    public static DateWindowResult Resolve(DateTimeOffset? from, DateTimeOffset? to, TimeProvider time, int defaultDays, int maxDays)
    {
        DateTimeOffset toUtc = (to ?? time.GetUtcNow()).ToUniversalTime();
        DateTimeOffset fromUtc = (from ?? toUtc.AddDays(-defaultDays)).ToUniversalTime();

        if (fromUtc > toUtc || toUtc - fromUtc > TimeSpan.FromDays(maxDays))
        {
            return new DateWindowResult(null, "from");
        }

        return new DateWindowResult(new DateWindow(fromUtc, toUtc), null);
    }
}

public sealed record DateWindowResult(DateWindow? Window, string? ErrorParameter);
