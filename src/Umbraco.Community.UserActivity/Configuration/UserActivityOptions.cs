namespace Umbraco.Community.UserActivity.Configuration;

/// <summary>Settings under <c>UserActivity</c> (design 5). Defaults are the privacy-first ones Pascal chose.</summary>
public sealed class UserActivityOptions
{
    public const string SectionName = "UserActivity";

    /// <summary>
    /// User group aliases (case-insensitive) that may read everyone's activity. Aliases, because group keys differ per
    /// site. Null by default: the configuration binder appends to an initialised array, so a default here could never
    /// be removed; <see cref="EffectiveAllowedUserGroups"/> applies it.
    /// </summary>
    public string[]? AllowedUserGroups { get; set; }

    /// <summary>The configured groups, or <c>admin</c> when none are configured.</summary>
    public IReadOnlyList<string> EffectiveAllowedUserGroups => AllowedUserGroups ?? ["admin"];

    /// <summary>Lets a non-admin open their own Activity tab (never the global view, never anyone else's).</summary>
    public bool AllowSelfView { get; set; }

    public int DefaultRangeDays { get; set; } = 30;

    public int MaxRangeDays { get; set; } = 366;

    public int MaxDepth { get; set; } = 5000;

    public int SourceTimeoutSeconds { get; set; } = 5;

    /// <summary>Off by default (Pascal, P8): core keeps the IP either way; this only controls our views and export.</summary>
    public bool ShowIpAddresses { get; set; }

    public bool LogViews { get; set; } = true;

    public ExportOptions Export { get; set; } = new();

    /// <summary>Per-source switches, keyed by source alias ignoring case (<c>Sources:AI</c> = <c>Sources:ai</c>).</summary>
    public Dictionary<string, SourceOptions> Sources { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Reserved for the recorder (next minor). 0 keeps rows forever.</summary>
    public RecorderOptions Recorder { get; set; } = new();
}

public sealed class ExportOptions
{
    public bool Enabled { get; set; }

    public int MaxRows { get; set; } = 10000;
}

public sealed class RecorderOptions
{
    /// <summary>The recorder's retention setting: events older than this many days are deleted daily. 0 keeps rows forever (the default, as Forms Audit Trail; the README tells site owners to set a limit). Maximum 36500.</summary>
    public int RetentionDays { get; set; }
}
