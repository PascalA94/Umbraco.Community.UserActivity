#if UMBRACO_17
using System.Reflection;
using System.Runtime.CompilerServices;
using UrlTracker.Core.Notifications;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.Recorder.Handlers.UrlTracker;

/// <summary>
/// Decides at compose time whether UrlTracker's handlers are registered (design 3.2, 4.1). Line 17 only: UrlTracker has
/// no stable 18.x release (18.0.0-beta.3 is the newest), so line 18 compiles none of this and a site running the beta
/// is simply not recorded (M5 handler probe, docs/spikes.md). Strings and reflection only; registration is a separate
/// non-inlined method.
/// </summary>
internal static class UrlTrackerGuard
{
    public const string AssemblyName = "UrlTracker.Core";

    private const string Notifications = "UrlTracker.Core.Notifications.";
    private const string Redirect = "UrlTracker.Core.Database.Entities.IRedirect";

    internal static IReadOnlyList<MemberSpec> Members { get; } =
    [
        MemberSpec.Type(Notifications + "RedirectCreatedNotification"),
        MemberSpec.Type(Notifications + "RedirectUpdatedNotification"),
        MemberSpec.Type(Notifications + "RedirectDeletedNotification"),
        MemberSpec.Property(Notifications + "RedirectNotificationBase", "Redirects"),
        MemberSpec.Property(Redirect, "SourceValue", "System.String"),
        MemberSpec.Property(Redirect, "Permanent", "System.Boolean"),
        MemberSpec.Property(Redirect, "Force", "System.Boolean"),
        MemberSpec.Property(Redirect, "RetainQuery", "System.Boolean"),
        MemberSpec.Property(Redirect, "Advanced", "System.Boolean"),
        MemberSpec.Property(Redirect, "Key", "System.Guid"),
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
            handlers.Register(RecorderSources.UrlTracker);
        }

        return result;
    }

    internal static SourceAvailabilityResult Evaluate(Assembly? assembly) => RecorderGuardSupport.Evaluate(RecorderSources.UrlTracker, assembly, Members);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register(IUmbracoBuilder builder)
        => builder.Services
            .AddRecorderHandler<RedirectCreatedNotification, UrlTrackerHandlers>(h => h.OnCreated)
            .AddRecorderHandler<RedirectUpdatedNotification, UrlTrackerHandlers>(h => h.OnUpdated)
            .AddRecorderHandler<RedirectDeletedNotification, UrlTrackerHandlers>(h => h.OnDeleted);
}
#endif
