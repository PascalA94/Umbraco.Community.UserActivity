namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// The rows other sources own (design 3.6), read at query time from every registered and available source that
/// implements <see cref="IClaimsCoreAuditRows"/>. Read lazily because the core source is itself in the collection.
/// </summary>
public sealed class CoreClaims
{
    private readonly Func<IEnumerable<IClaimsCoreAuditRows>> _claimants;

    public CoreClaims(Func<IEnumerable<IClaimsCoreAuditRows>> claimants) => _claimants = claimants;

    /// <summary>Ordinal <c>eventType</c> prefixes.</summary>
    public IReadOnlyList<string> AuditPrefixes => _claimants().SelectMany(c => c.ClaimedAuditEventTypePrefixes).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Lower-cased <c>entityType</c> values.</summary>
    public IReadOnlyList<string> LogEntityTypes => _claimants().SelectMany(c => c.ClaimedLogEntityTypes).Select(e => e.ToLowerInvariant()).Distinct().ToList();
}
