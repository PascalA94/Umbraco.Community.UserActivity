using System.Reflection;
using System.Runtime.CompilerServices;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.UserActivity.Sources.Adapters.USync;

/// <summary>
/// Decides at compose time whether the <c>usync</c> source exists (design 3.2), the same way as <see cref="AI.AIGuard"/>:
/// strings and reflection only, then a separate non-inlined registration. uSync.History 17.0.x has no
/// <c>ISyncHistoryService</c> (S7), so the probe fails there and the source stays hidden.
/// </summary>
internal static class USyncGuard
{
    public const string AssemblyName = "uSync.History";

    private const string Service = "uSync.History.Service.ISyncHistoryService";
    private const string Info = "uSync.History.HistoryInfo";

    internal static IReadOnlyList<MemberSpec> Members { get; } =
    [
        MemberSpec.Type(Service),
        MemberSpec.Method(Service, "GetHistoryAsync"),
        MemberSpec.Method(Service, "IsEnabled"),
        MemberSpec.Property(Info, "Date", "System.DateTime"),
        MemberSpec.Property(Info, "Username", "System.String"),
        MemberSpec.Property(Info, "Method", "System.String"),
        MemberSpec.Property(Info, "Changes", "System.Int32"),
        MemberSpec.Property(Info, "Total", "System.Int32"),
        MemberSpec.Property(Info, "FilePath", "System.String"),
    ];

    public static SourceAvailabilityResult TryRegister(IUmbracoBuilder builder)
    {
        SourceAvailabilityResult result = Evaluate(OptionalAssemblyGuard.Find(AssemblyName));
        if (result.State == AdapterState.Registered)
        {
            Register(builder);
        }

        return result;
    }

    internal static SourceAvailabilityResult Evaluate(Assembly? assembly)
    {
        if (assembly is null)
        {
            return new SourceAvailabilityResult(SourceAlias.USync, AdapterState.NotInstalled, null, null);
        }

        var version = assembly.GetName().Version?.ToString();
        ProbeResult probe = AdapterProbe.Check(assembly, Members);
        return probe.Ok
            ? new SourceAvailabilityResult(SourceAlias.USync, AdapterState.Registered, version, null)
            : new SourceAvailabilityResult(SourceAlias.USync, AdapterState.ProbeFailed, version, probe.MissingMember);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register(IUmbracoBuilder builder) => builder.UserActivitySources().Append<USyncSource>();
}
