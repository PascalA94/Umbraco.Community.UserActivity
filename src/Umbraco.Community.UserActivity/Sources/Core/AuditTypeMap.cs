using Umbraco.Cms.Core.Models;

namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// <c>umbracoLog.logHeader</c> (an <see cref="AuditType"/> name) to a category. Explicit for every value, so a new
/// AuditType in a CMS minor fails <c>AuditTypeMap_EveryAuditType_HasCategory</c> until someone maps it. Headers
/// we don't know (other packages' strings) are <c>other</c>.
/// </summary>
internal static class AuditTypeMap
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["New"] = Categories.Created,
        ["Save"] = Categories.Saved,
        ["SaveVariant"] = Categories.Saved,
        ["Open"] = Categories.Other,
        ["Delete"] = Categories.Deleted,
        ["Publish"] = Categories.Published,
        ["PublishVariant"] = Categories.Published,
        ["SendToPublish"] = Categories.Other,
        ["SendToPublishVariant"] = Categories.Other,
        ["Unpublish"] = Categories.Unpublished,
        ["UnpublishVariant"] = Categories.Unpublished,
        // Core logs a move to the recycle bin as Move too; telling them apart would mean parsing comment text.
        ["Move"] = Categories.Moved,
        ["Copy"] = Categories.Copied,
        ["AssignDomain"] = Categories.Other,
        ["PublicAccess"] = Categories.PermissionsChanged,
        ["Sort"] = Categories.Sorted,
        ["Notify"] = Categories.Other,
        ["System"] = Categories.Other,
        ["RollBack"] = Categories.RolledBack,
        ["PackagerInstall"] = Categories.Other,
        ["PackagerUninstall"] = Categories.Other,
        ["Custom"] = Categories.Other,
        ["ContentVersionPreventCleanup"] = Categories.Other,
        ["ContentVersionEnableCleanup"] = Categories.Other,
    };

    /// <summary>Lower-cased headers whose category is not <c>other</c>; "other" means "none of these".</summary>
    public static IReadOnlyList<string> NonOtherHeaders { get; } =
        Map.Where(p => p.Value != Categories.Other).Select(p => p.Key.ToLowerInvariant()).ToList();

    public static bool IsMapped(string header) => Map.ContainsKey(header);

    public static string ToCategory(string? header)
        => header is not null && Map.TryGetValue(header, out var category) ? category : Categories.Other;

    /// <summary>Lower-cased headers for a category other than <c>other</c>.</summary>
    public static IReadOnlyList<string> HeadersFor(string category)
        => category == Categories.Other ? [] : Map.Where(p => p.Value == category).Select(p => p.Key.ToLowerInvariant()).ToList();
}
