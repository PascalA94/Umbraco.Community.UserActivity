using System.Reflection;
using System.Runtime.CompilerServices;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.UserActivity.Sources.Adapters.AI;

/// <summary>
/// Decides at compose time whether the <c>ai</c> source exists (design 3.2). Strings and reflection only: this class
/// never names an Umbraco.AI type outside <see cref="Register"/>, which is a separate non-inlined method, so a site
/// without Umbraco.AI never JIT-compiles anything that binds to it and can't hit a TypeLoadException at start-up.
/// </summary>
internal static class AIGuard
{
    public const string AssemblyName = "Umbraco.AI.Core";

    private const string AuditLog = "Umbraco.AI.Core.AuditLog.";
    private const string Service = AuditLog + "IAIAuditLogService";
    private const string Log = AuditLog + "AIAuditLog";
    private const string Filter = AuditLog + "AIAuditLogFilter";

    /// <summary>Every member <see cref="AISource"/> calls, with the signatures S7 found on all 32 releases (17.0.0-18.7.0).</summary>
    internal static IReadOnlyList<MemberSpec> Members { get; } =
    [
        MemberSpec.Type(Service),
        MemberSpec.Method(Service, "GetAuditLogsPagedAsync", Filter, "System.Int32", "System.Int32", "System.Threading.CancellationToken"),
        MemberSpec.Method(Service, "GetAuditLogAsync", "System.Guid", "System.Threading.CancellationToken"),
        MemberSpec.Property(Log, "Id", "System.Guid"),
        MemberSpec.Property(Log, "StartTime", "System.DateTime"),
        MemberSpec.Property(Log, "UserId", "System.String"),
        MemberSpec.Property(Log, "UserName", "System.String"),
        MemberSpec.Property(Log, "EntityId", "System.String"),
        MemberSpec.Property(Log, "EntityType", "System.String"),
        MemberSpec.Property(Log, "FeatureType", "System.String"),
        MemberSpec.Property(Log, "Status", AuditLog + "AIAuditLogStatus"),
        MemberSpec.Property(Log, "ProfileAlias", "System.String"),
        MemberSpec.Property(Log, "ProviderId", "System.String"),
        MemberSpec.Property(Log, "ModelId", "System.String"),
        MemberSpec.Property(Filter, "UserId", "System.String"),
        MemberSpec.Property(Filter, "FromDate"),
        MemberSpec.Property(Filter, "ToDate"),
        MemberSpec.Property(Filter, "SearchText", "System.String"),
        MemberSpec.Property(AuditLog + "AIAuditLogOptions", "RetentionDays", "System.Int32"),
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
            return new SourceAvailabilityResult(SourceAlias.AI, AdapterState.NotInstalled, null, null);
        }

        var version = assembly.GetName().Version?.ToString();
        ProbeResult probe = AdapterProbe.Check(assembly, Members);
        return probe.Ok
            ? new SourceAvailabilityResult(SourceAlias.AI, AdapterState.Registered, version, null)
            : new SourceAvailabilityResult(SourceAlias.AI, AdapterState.ProbeFailed, version, probe.MissingMember);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register(IUmbracoBuilder builder) => builder.UserActivitySources().Append<AISource>();
}
