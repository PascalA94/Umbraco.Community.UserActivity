using System.Text.RegularExpressions;

namespace Umbraco.Community.UserActivity.Sources;

/// <summary>Source aliases are permanent API values and config keys, so their shape is fixed.</summary>
public static partial class SourceAlias
{
    public const string Core = "core";
    public const string Automate = "automate";
    public const string Forms = "forms";
    public const string AI = "ai";
    public const string USync = "usync";
    public const string ArchivedUsers = "archivedUsers";
    public const string FormsAuditTrail = "formsAuditTrail";
    public const string Recorder = "recorder";

    public static bool IsValid(string? alias) => alias is not null && Pattern().IsMatch(alias);

    [GeneratedRegex("^[a-z][a-zA-Z0-9]{1,31}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
