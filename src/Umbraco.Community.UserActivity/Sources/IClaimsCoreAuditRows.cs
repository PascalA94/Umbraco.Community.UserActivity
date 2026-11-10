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

    /// <summary>
    /// Part of an entity type's <c>umbracoLog</c> rows, when the whole type is too broad: Archived Users claims
    /// <c>User</c> rows with header <c>Custom</c> whose comment starts <c>ArchivedUsers:</c>. Empty by default.
    /// </summary>
    IReadOnlyCollection<ClaimedLogRows> ClaimedLogRows => [];
}

/// <summary>
/// <c>umbracoLog</c> rows with this entity type and header (ignoring case) whose comment starts with
/// <paramref name="CommentPrefix"/> (ignoring case, as SQL <c>LIKE</c> does on default collations).
/// </summary>
public sealed record ClaimedLogRows(string EntityType, string LogHeader, string CommentPrefix);
