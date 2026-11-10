using System.Reflection;
using System.Runtime.CompilerServices;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.CSPManager.Notifications;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.Recorder.Handlers.CspManager;

/// <summary>
/// Decides at compose time whether CSP Manager's handler is registered (design 3.2, 4.1): strings and reflection only,
/// then a separate non-inlined registration, exactly like the source adapters. The notification is the same in every
/// release on both lines (M5 handler probe, docs/spikes.md).
/// </summary>
internal static class CspManagerGuard
{
    public const string AssemblyName = "Umbraco.Community.CSPManager";

    private const string Notification = "Umbraco.Community.CSPManager.Notifications.CspSavedNotification";
    private const string Definition = "Umbraco.Community.CSPManager.Models.CspDefinition";

    internal static IReadOnlyList<MemberSpec> Members { get; } =
    [
        MemberSpec.Type(Notification),
        MemberSpec.Property(Notification, "CspDefinition", Definition),
        MemberSpec.Property(Definition, "Id", "System.Guid"),
        MemberSpec.Property(Definition, "Enabled", "System.Boolean"),
        MemberSpec.Property(Definition, "ReportOnly", "System.Boolean"),
        MemberSpec.Property(Definition, "IsBackOffice", "System.Boolean"),
        MemberSpec.Property(Definition, "Sources"),
    ];

    public static SourceAvailabilityResult TryRegister(IUmbracoBuilder builder, RecorderHandlerRegistry handlers)
        => TryRegister(builder, handlers, OptionalAssemblyGuard.Find(AssemblyName));

    /// <summary>The assembly lookup is injectable so tests can drive both outcomes without the package being loaded.</summary>
    internal static SourceAvailabilityResult TryRegister(IUmbracoBuilder builder, RecorderHandlerRegistry handlers, Assembly? assembly)
    {
        SourceAvailabilityResult result = Evaluate(assembly);
        if (result.State == AdapterState.Registered)
        {
            Register(builder);
            handlers.Register(RecorderSources.Csp);
        }

        return result;
    }

    internal static SourceAvailabilityResult Evaluate(Assembly? assembly) => RecorderGuardSupport.Evaluate(RecorderSources.Csp, assembly, Members);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register(IUmbracoBuilder builder)
        => builder.Services.AddRecorderHandler<CspSavedNotification, CspHandlers>(h => h.OnSaved);
}
