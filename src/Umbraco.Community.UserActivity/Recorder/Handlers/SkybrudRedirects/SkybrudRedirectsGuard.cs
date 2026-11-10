using System.Reflection;
using System.Runtime.CompilerServices;
using Skybrud.Umbraco.Redirects.Notifications;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.Recorder.Handlers.SkybrudRedirects;

/// <summary>
/// Decides at compose time whether Skybrud Redirects' handlers are registered (design 3.2, 4.1). The added, saved and
/// deleted notifications first ship in 17.0.5 (the 17.0.0-17.0.4 releases only have lookup notifications), so the probe
/// hides the recorder for Skybrud on those and the compile reference is 17.0.5, the same boundary treatment as uSync
/// (M5 handler probe, docs/spikes.md). Strings and reflection only; registration is a separate non-inlined method.
/// </summary>
internal static class SkybrudRedirectsGuard
{
    public const string AssemblyName = "Skybrud.Umbraco.Redirects";

    private const string Notifications = "Skybrud.Umbraco.Redirects.Notifications.";
    private const string Redirect = "Skybrud.Umbraco.Redirects.Models.IRedirect";

    internal static IReadOnlyList<MemberSpec> Members { get; } =
    [
        MemberSpec.Type(Notifications + "RedirectAddedNotification"),
        MemberSpec.Property(Notifications + "RedirectAddedNotification", "Redirect", Redirect),
        MemberSpec.Type(Notifications + "RedirectSavedNotification"),
        MemberSpec.Property(Notifications + "RedirectSavedNotification", "Redirect", Redirect),
        MemberSpec.Type(Notifications + "RedirectDeletedNotification"),
        MemberSpec.Property(Notifications + "RedirectDeletedNotification", "Redirect", Redirect),
        MemberSpec.Property(Redirect, "Key", "System.Guid"),
        MemberSpec.Property(Redirect, "Path", "System.String"),
        MemberSpec.Property(Redirect, "Type"),
        MemberSpec.Property(Redirect, "IsPermanent", "System.Boolean"),
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
            handlers.Register(RecorderSources.SkybrudRedirects);
        }

        return result;
    }

    internal static SourceAvailabilityResult Evaluate(Assembly? assembly) => RecorderGuardSupport.Evaluate(RecorderSources.SkybrudRedirects, assembly, Members);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register(IUmbracoBuilder builder)
        => builder.Services
            .AddRecorderHandler<RedirectAddedNotification, SkybrudRedirectHandlers>(h => h.OnAdded)
            .AddRecorderHandler<RedirectSavedNotification, SkybrudRedirectHandlers>(h => h.OnSaved)
            .AddRecorderHandler<RedirectDeletedNotification, SkybrudRedirectHandlers>(h => h.OnDeleted);
}
