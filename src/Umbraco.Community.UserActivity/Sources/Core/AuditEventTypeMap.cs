namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// <c>umbracoAudit.eventType</c> to a category and entity type. The strings are core's own, identical in the
/// 17.8.0 and 18.3.0 assemblies (S5). Unknown event types are <c>other</c>.
/// </summary>
internal static class AuditEventTypeMap
{
    public const string SignInLogin = "umbraco/user/sign-in/login";
    public const string SignInLogout = "umbraco/user/sign-in/logout";
    public const string SignInFailed = "umbraco/user/sign-in/failed";

    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        [SignInLogin] = Categories.SignedIn,
        [SignInLogout] = Categories.SignedOut,
        [SignInFailed] = Categories.SignInFailed,
        ["umbraco/user/password/reset"] = Categories.PasswordReset,
        ["umbraco/user/password/change"] = Categories.PasswordChanged,
        ["umbraco/user/password/forgot/change"] = Categories.PasswordReset,
        ["umbraco/user/password/forgot/request"] = Categories.Other,
        ["umbraco/user/save"] = Categories.Saved,
        ["umbraco/user/delete"] = Categories.Deleted,
        ["umbraco/user-group/save"] = Categories.Saved,
        ["umbraco/user-group/permissions-change"] = Categories.PermissionsChanged,
        ["umbraco/member/save"] = Categories.Saved,
        ["umbraco/member/delete"] = Categories.Deleted,
        ["umbraco/member/exported"] = Categories.Exported,
        ["umbraco/member/roles/assigned"] = Categories.PermissionsChanged,
        ["umbraco/member/roles/removed"] = Categories.PermissionsChanged,
        ["community/useractivity/viewed"] = Categories.Other,
        ["community/useractivity/exported"] = Categories.Exported,
    };

    /// <summary>Entity type by event type prefix. <c>umbraco/user/</c> is not a prefix of <c>umbraco/user-group/</c>.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> EntityTypePrefixes { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["user"] = ["umbraco/user/", "community/useractivity/"],
        ["userGroup"] = ["umbraco/user-group/"],
        ["member"] = ["umbraco/member/"],
    };

    public static IReadOnlyList<string> NonOtherEventTypes { get; } = Map.Where(p => p.Value != Categories.Other).Select(p => p.Key).ToList();

    public static string ToCategory(string eventType) => Map.TryGetValue(eventType, out var category) ? category : Categories.Other;

    public static string? ToEntityType(string eventType)
        => EntityTypePrefixes.FirstOrDefault(p => p.Value.Any(prefix => eventType.StartsWith(prefix, StringComparison.Ordinal))).Key;

    public static IReadOnlyList<string> EventTypesFor(string category)
        => category == Categories.Other ? [] : Map.Where(p => p.Value == category).Select(p => p.Key).ToList();
}
