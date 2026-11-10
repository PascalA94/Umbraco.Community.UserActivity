namespace Umbraco.Community.UserActivity.Sources;

/// <summary>
/// A place activity is read from (design 3.3). Public and versioned with the package: stable within a major.
/// Register with <c>builder.UserActivitySources().Append&lt;MySource&gt;()</c>; every source also gets a
/// <c>UserActivity:Sources:&lt;Alias&gt;:Enabled</c> setting.
/// </summary>
public interface IUserActivitySource
{
    /// <summary>Permanent, <c>^[a-z][a-zA-Z0-9]{1,31}$</c> (see <see cref="SourceAlias"/>).</summary>
    string Alias { get; }

    /// <summary>Client localisation key, e.g. <c>userActivity_sourceCore</c>.</summary>
    string DisplayNameKey { get; }

    /// <summary>False hides the source everywhere (package not installed, probe failed).</summary>
    bool IsAvailable { get; }

    ActivitySourceInfo Info { get; }

    /// <summary>The top <see cref="ActivitySourceQuery.Limit"/> rows in the window. Never throws for bad stored data: skip the row.</summary>
    Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct);

    /// <summary>
    /// One event by its opaque id, with <see cref="ActivityEvent.Details"/>; null when absent. Details are shown to
    /// readers only and must never hold an IP address: <see cref="ActivityEvent.IpAddress"/> is the one place for it,
    /// so <c>ShowIpAddresses</c> controls it.
    /// </summary>
    Task<ActivityEvent?> GetAsync(string id, CancellationToken ct);
}
