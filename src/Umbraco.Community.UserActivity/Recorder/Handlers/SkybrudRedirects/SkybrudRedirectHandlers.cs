using Skybrud.Umbraco.Redirects.Models;
using Skybrud.Umbraco.Redirects.Notifications;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Recorder.Handlers.SkybrudRedirects;

/// <summary>
/// Skybrud Redirects' add, edit and delete (design 4.1). Each is raised from <c>RedirectsService</c> after the change
/// is stored; delete runs inside Skybrud's own scope (S4), which the writer joins safely.
/// Allow-listed details: the redirect type and whether it is permanent. The redirect's path is the entity name, cut
/// before any query string; the query string and the destination are never read, because they can carry tokens.
/// </summary>
internal sealed class SkybrudRedirectHandlers
{
    private readonly RecorderHandlerContext _context;

    public SkybrudRedirectHandlers(RecorderHandlerContext context) => _context = context;

    public void OnAdded(RedirectAddedNotification notification)
        => _context.Record(RecorderSources.SkybrudRedirects, () => [ToEntry(Categories.Created, nameof(RedirectAddedNotification), notification.Redirect)]);

    public void OnSaved(RedirectSavedNotification notification)
        => _context.Record(RecorderSources.SkybrudRedirects, () => [ToEntry(Categories.Saved, nameof(RedirectSavedNotification), notification.Redirect)]);

    public void OnDeleted(RedirectDeletedNotification notification)
        => _context.Record(RecorderSources.SkybrudRedirects, () => [ToEntry(Categories.Deleted, nameof(RedirectDeletedNotification), notification.Redirect)]);

    private static RecorderEntry ToEntry(string category, string rawType, IRedirect redirect)
        => new(
            RecorderSources.SkybrudRedirects,
            category,
            rawType,
            null,
            null,
            "redirect",
            redirect.Key.ToString(),
            RecorderDetails.PathOnly(redirect.Path),
            new Dictionary<string, string>
            {
                ["type"] = redirect.Type.ToString(),
                ["permanent"] = RecorderDetails.Flag(redirect.IsPermanent),
            });
}
