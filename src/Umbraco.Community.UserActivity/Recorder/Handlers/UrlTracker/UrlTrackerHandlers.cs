#if UMBRACO_17
using UrlTracker.Core.Notifications;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Recorder.Handlers.UrlTracker;

/// <summary>
/// UrlTracker's created, updated and deleted redirects (design 4.1). One notification can carry several redirects
/// (an import, a bulk delete), so each becomes its own row, up to <see cref="MaxRowsPerNotification"/> so a large import
/// cannot flood the table through one save.
/// Allow-listed details: the permanent, force, retain-query and advanced flags. The source value is the entity name,
/// cut before any query string, or a fixed label for an advanced (regex) redirect; the target is never read. More than
/// the cap adds one row carrying a <c>truncated</c> count.
/// </summary>
internal sealed class UrlTrackerHandlers
{
    internal const int MaxRowsPerNotification = RecorderDetails.MaxRowsPerNotification;

    private readonly RecorderHandlerContext _context;

    public UrlTrackerHandlers(RecorderHandlerContext context) => _context = context;

    public void OnCreated(RedirectCreatedNotification notification)
        => _context.Record(RecorderSources.UrlTracker, () => ToEntries(Categories.Created, nameof(RedirectCreatedNotification), notification));

    public void OnUpdated(RedirectUpdatedNotification notification)
        => _context.Record(RecorderSources.UrlTracker, () => ToEntries(Categories.Saved, nameof(RedirectUpdatedNotification), notification));

    public void OnDeleted(RedirectDeletedNotification notification)
        => _context.Record(RecorderSources.UrlTracker, () => ToEntries(Categories.Deleted, nameof(RedirectDeletedNotification), notification));

    // A regex can embed a query token without a literal "?" (for example \x3F), so PathOnly cannot make an advanced
    // redirect's pattern safe: store a fixed label instead of the pattern.
    private const string PatternLabel = "Pattern redirect";

    private static IEnumerable<RecorderEntry> ToEntries(string category, string rawType, RedirectNotificationBase notification)
        => RecorderDetails.Capped(
            notification.Redirects,
            redirect => new RecorderEntry(
                RecorderSources.UrlTracker,
                category,
                rawType,
                null,
                null,
                "redirect",
                redirect.Key.ToString(),
                redirect.Advanced ? PatternLabel : RecorderDetails.PathOnly(redirect.SourceValue),
                new Dictionary<string, string>
                {
                    ["permanent"] = RecorderDetails.Flag(redirect.Permanent),
                    ["force"] = RecorderDetails.Flag(redirect.Force),
                    ["retainQuery"] = RecorderDetails.Flag(redirect.RetainQuery),
                    ["advanced"] = RecorderDetails.Flag(redirect.Advanced),
                }),
            dropped => new RecorderEntry(RecorderSources.UrlTracker, category, rawType, null, null, "redirect", null, null, RecorderDetails.Truncated(dropped)));
}
#endif
