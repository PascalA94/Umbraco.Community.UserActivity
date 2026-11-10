using Microsoft.Extensions.Logging;

namespace Umbraco.Community.UserActivity.Sources.Adapters;

internal enum AdapterState
{
    /// <summary>The integration's assembly isn't loaded: the source is not registered, so it is hidden.</summary>
    NotInstalled,

    /// <summary>Installed and every member the adapter calls is there: the source is registered.</summary>
    Registered,

    /// <summary>Installed, but a member changed: not registered, one Warning at start-up.</summary>
    ProbeFailed,
}

/// <summary>What a runtime adapter's guard found at compose time (design 3.2). Names and versions only, safe to log.</summary>
/// <param name="AssemblyVersion">The installed integration's assembly version, when installed.</param>
/// <param name="MissingMember">The first type or member the probe didn't find.</param>
internal sealed record SourceAvailabilityResult(string Alias, AdapterState State, string? AssemblyVersion, string? MissingMember);

/// <summary>Every guard's result, kept so start-up can log the probe failures once (the composer has no logger).</summary>
internal sealed class AdapterRegistrations
{
    private readonly List<SourceAvailabilityResult> _results = [];

    public IReadOnlyList<SourceAvailabilityResult> Results => _results;

    public void Add(SourceAvailabilityResult result) => _results.Add(result);

    public void LogProblems(ILogger logger)
    {
        foreach (SourceAvailabilityResult result in _results.Where(r => r.State == AdapterState.ProbeFailed))
        {
            logger.LogWarning(
                "User Activity source {SourceAlias} is unavailable: the installed integration (assembly version {AssemblyVersion}) has no {MissingMember}.",
                result.Alias, result.AssemblyVersion, result.MissingMember);
        }
    }
}

/// <summary>A source backed by an optional integration, so failures can name the integration's version.</summary>
internal interface IRuntimeAdapter
{
    /// <summary>The integration's assembly version, or null if it can't be read.</summary>
    string? IntegrationVersion { get; }
}
