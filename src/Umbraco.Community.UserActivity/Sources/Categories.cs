namespace Umbraco.Community.UserActivity.Sources;

/// <summary>Normalised categories (design 3.4). Permanent: adding is fine, renaming is breaking.</summary>
public static class Categories
{
    public const string Created = "created";
    public const string Saved = "saved";
    public const string Published = "published";
    public const string Unpublished = "unpublished";
    public const string Deleted = "deleted";
    public const string Trashed = "trashed";
    public const string Restored = "restored";
    public const string Moved = "moved";
    public const string Copied = "copied";
    public const string Sorted = "sorted";
    public const string RolledBack = "rolledBack";
    public const string SignedIn = "signedIn";
    public const string SignInFailed = "signInFailed";
    public const string SignedOut = "signedOut";
    public const string PasswordChanged = "passwordChanged";
    public const string PasswordReset = "passwordReset";
    public const string PermissionsChanged = "permissionsChanged";
    public const string Imported = "imported";
    public const string Exported = "exported";
    public const string Ran = "ran";
    public const string Other = "other";

    public static IReadOnlyList<string> All { get; } =
    [
        Created, Saved, Published, Unpublished, Deleted, Trashed, Restored, Moved, Copied, Sorted, RolledBack,
        SignedIn, SignInFailed, SignedOut, PasswordChanged, PasswordReset, PermissionsChanged, Imported, Exported,
        Ran, Other,
    ];

    public static bool IsKnown(string category) => All.Contains(category, StringComparer.Ordinal);
}
