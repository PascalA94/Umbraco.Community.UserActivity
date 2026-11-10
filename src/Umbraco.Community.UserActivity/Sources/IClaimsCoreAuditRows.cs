namespace Umbraco.Community.UserActivity.Sources;

/// <summary>
/// De-duplication by ownership (design 3.6): a source that reads some of core's <c>umbracoAudit</c> or
/// <c>umbracoLog</c> rows claims them, and the core source skips them. Two sources may not claim the same rows.
/// </summary>
public interface IClaimsCoreAuditRows
{
    /// <summary><c>umbracoAudit.eventType</c> prefixes, ordinal, e.g. <c>umbraco/automate/</c>.</summary>
    IReadOnlyCollection<string> ClaimedAuditEventTypePrefixes { get; }

    /// <summary><c>umbracoLog.entityType</c> values, ignoring case, e.g. <c>Form</c>.</summary>
    IReadOnlyCollection<string> ClaimedLogEntityTypes { get; }
}
