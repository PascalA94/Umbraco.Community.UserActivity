using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.UserActivity.Configuration;

namespace Umbraco.Community.UserActivity.Sources;

/// <summary>
/// Which sources take part (design 3.2): enabled by <c>UserActivity:Sources:&lt;Name&gt;:Enabled</c> (default on)
/// <b>and</b> available (their package is installed and its probe passed). Everything that lists or queries
/// sources (engine, filter options, API validation, core claims) goes through here, so a disabled or missing source
/// disappears everywhere at once. Installed means available: nothing else is checked (Pascal, P5).
/// </summary>
public sealed class SourceAvailability
{
    /// <summary>Names we ship or reserve. They never warn, so one config file works on sites with different packages.</summary>
    private static readonly string[] BuiltInNames =
    [
        SourceAlias.Core, SourceAlias.Automate, SourceAlias.Forms, SourceAlias.AI, SourceAlias.USync,
        SourceAlias.ArchivedUsers, SourceAlias.FormsAuditTrail, SourceAlias.Recorder,
    ];

    private readonly UserActivitySourceCollection _sources;
    private readonly IOptionsMonitor<UserActivityOptions> _options;

    public SourceAvailability(UserActivitySourceCollection sources, IOptionsMonitor<UserActivityOptions> options)
    {
        _sources = sources;
        _options = options;
    }

    /// <summary>Every registered source, active or not.</summary>
    public IEnumerable<IUserActivitySource> All => _sources;

    public bool IsEnabled(string alias)
        => !_options.CurrentValue.Sources.TryGetValue(alias, out SourceOptions? source) || source is null || source.Enabled;

    /// <summary>Enabled and available sources, in registration order.</summary>
    public IReadOnlyList<IUserActivitySource> GetActive()
        => _sources.Where(s => IsEnabled(s.Alias) && IsAvailable(s)).ToList();

    /// <summary>
    /// A source whose <see cref="IUserActivitySource.IsAvailable"/> throws (an integration that changed shape, a
    /// third-party bug) counts as unavailable, so one source can't fail every Activity request (M3 review QA-1).
    /// </summary>
    private static bool IsAvailable(IUserActivitySource source)
    {
        try
        {
            return source.IsAvailable;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The active source with this alias (ordinal: aliases are lower camel case API values), or null.</summary>
    public IUserActivitySource? GetActive(string alias)
        => GetActive().FirstOrDefault(s => string.Equals(s.Alias, alias, StringComparison.Ordinal));

    /// <summary>One Warning per <c>Sources</c> entry that names no registered or built-in source. Never an error.</summary>
    public void LogUnknownSettings(ILogger logger)
    {
        var known = new HashSet<string>(BuiltInNames.Concat(_sources.Select(s => s.Alias)), StringComparer.OrdinalIgnoreCase);
        foreach (var name in _options.CurrentValue.Sources.Keys.Where(n => !known.Contains(n)))
        {
            logger.LogWarning("UserActivity:Sources has a setting for {SourceName}, but there is no source called that.", name);
        }
    }
}
