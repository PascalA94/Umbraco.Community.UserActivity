using System.Reflection;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.Recorder.Handlers;

internal static class RecorderGuardSupport
{
    /// <summary>The guard decision every recorded package shares: not loaded, probe failed (first missing member named), or fine.</summary>
    public static SourceAvailabilityResult Evaluate(string name, Assembly? assembly, IReadOnlyList<MemberSpec> members)
    {
        if (assembly is null)
        {
            return new SourceAvailabilityResult(name, AdapterState.NotInstalled, null, null);
        }

        var version = assembly.GetName().Version?.ToString();
        ProbeResult probe = AdapterProbe.Check(assembly, members);
        return probe.Ok
            ? new SourceAvailabilityResult(name, AdapterState.Registered, version, null)
            : new SourceAvailabilityResult(name, AdapterState.ProbeFailed, version, probe.MissingMember);
    }
}
