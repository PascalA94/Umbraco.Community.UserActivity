namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// The rows other sources own (design 3.6), read at query time from every registered and available source that
/// implements <see cref="IClaimsCoreAuditRows"/>. Read lazily because the core source is itself in the collection.
/// </summary>
public sealed class CoreClaims
{
    private readonly Func<IEnumerable<IClaimsCoreAuditRows>> _claimants;

    public CoreClaims(Func<IEnumerable<IClaimsCoreAuditRows>> claimants) => _claimants = claimants;

    /// <summary>
    /// The registered sources that claim rows and are available. Claimants are picked before asking
    /// <see cref="IUserActivitySource.IsAvailable"/>, because an adapter's IsAvailable may call into its integration
    /// (M3 review QA-1) and sources that claim nothing have no say here.
    /// </summary>
    /// <remarks>
    /// <paramref name="sources"/> is a factory and is only called at query time: the collection holds the core
    /// source, which needs this object, so reading it while this is built is a resolution cycle (it deadlocked boot).
    /// </remarks>
    internal static CoreClaims From(Func<UserActivitySourceCollection> sources)
        => new(() => sources().Where(s => s is not CoreSource).OfType<IClaimsCoreAuditRows>().Where(c => ((IUserActivitySource)c).IsAvailable));

    /// <summary>Ordinal <c>eventType</c> prefixes.</summary>
    public IReadOnlyList<string> AuditPrefixes => _claimants().SelectMany(c => c.ClaimedAuditEventTypePrefixes).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>Lower-cased <c>entityType</c> values.</summary>
    /// <summary>Partial <c>umbracoLog</c> claims (entity type, header, comment prefix).</summary>
    public IReadOnlyList<ClaimedLogRows> LogRows => _claimants().SelectMany(c => c.ClaimedLogRows).Distinct().ToList();

    public IReadOnlyList<string> LogEntityTypes => _claimants().SelectMany(c => c.ClaimedLogEntityTypes).Select(e => e.ToLowerInvariant()).Distinct().ToList();
}
