using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Controllers;
using Umbraco.Community.UserActivity.Export;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Recorder.Handlers;
using Umbraco.Community.UserActivity.Recorder.Handlers.CspManager;
using Umbraco.Community.UserActivity.Recorder.Handlers.SkybrudRedirects;
using Umbraco.Community.UserActivity.Recorder.Handlers.Workflow;
#if UMBRACO_17
using Umbraco.Community.UserActivity.Recorder.Handlers.UrlTracker;
#endif
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Community.UserActivity.Security;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Community.UserActivity.Sources.ArchivedUsers;
using Umbraco.Community.UserActivity.Sources.Adapters.AI;
using Umbraco.Community.UserActivity.Sources.Adapters.USync;
using Umbraco.Community.UserActivity.Sources.Automate;
using Umbraco.Community.UserActivity.Sources.Core;
using Umbraco.Community.UserActivity.Sources.Forms;
using Umbraco.Community.UserActivity.Web.OpenApi;
using Microsoft.AspNetCore.Authorization;

namespace Umbraco.Community.UserActivity.Web;

/// <summary>The package's only composer. Register services, handlers and options here.</summary>
public sealed class UserActivityComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<UserActivityOptions>()
            .Bind(builder.Config.GetSection(UserActivityOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<UserActivityOptions>, UserActivityOptionsValidator>();

        builder.UserActivitySources().Append<CoreSource>().Append<AutomateSource>().Append<FormsSource>().Append<ArchivedUsersSource>().Append<RecorderSource>();

        // The recorder (design 4): the table's migration plan is found by Umbraco's type scan. Each recorded package's
        // handler guard (M5 Tasks 32-33) calls handlers.Register(...) when its package is installed and passes its probe.
        var handlers = new RecorderHandlerRegistry();
        builder.Services.AddSingleton(handlers);
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<RecorderWriter>();
        builder.Services.AddSingleton<IRecorderWriter>(sp => sp.GetRequiredService<RecorderWriter>());
        builder.Services.AddTransient<RecorderHandlerContext>();
        builder.Services.AddSingleton<IDistributedBackgroundJob, RetentionJob>();
        builder.AddNotificationHandler<UmbracoApplicationStartingNotification, RecorderStartupNotice>();

        // Runtime adapters (design 3.2): each guard checks by name and reflection, then registers its source only if
        // the integration is installed and still has every member the adapter calls.
        var adapters = new AdapterRegistrations();
        adapters.Add(AdapterRegistrations.Safely(SourceAlias.AI, () => AIGuard.TryRegister(builder)));
        adapters.Add(AdapterRegistrations.Safely(SourceAlias.USync, () => USyncGuard.TryRegister(builder)));
        // Recorded packages (design 4.1): the same guard rule, and a guard that passes also registers its handler name.
        adapters.Add(AdapterRegistrations.Safely(RecorderSources.Csp, () => CspManagerGuard.TryRegister(builder, handlers)));
        adapters.Add(AdapterRegistrations.Safely(RecorderSources.Workflow, () => WorkflowGuard.TryRegister(builder, handlers)));
        adapters.Add(AdapterRegistrations.Safely(RecorderSources.SkybrudRedirects, () => SkybrudRedirectsGuard.TryRegister(builder, handlers)));
#if UMBRACO_17
        // UrlTracker has no stable 18.x release (docs/spikes.md, M5 handler probe), so line 18 compiles no guard for it.
        adapters.Add(AdapterRegistrations.Safely(RecorderSources.UrlTracker, () => UrlTrackerGuard.TryRegister(builder, handlers)));
#endif
        builder.Services.AddSingleton(adapters);
        builder.Services.AddSingleton<SourceFailures>();
        AddCoreClaims(builder.Services);
        builder.Services.AddSingleton<SourceAvailability>();
        builder.Services.AddTransient(sp => new UserIdentityResolver(
            sp.GetRequiredService<IUserService>(), sp.GetRequiredService<AppCaches>().RequestCache));
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddTransient<ActivityQueryEngine>();
        builder.Services.AddTransient<ActivityRequestParser>();
        builder.Services.AddSingleton<ExportGate>();
        builder.Services.AddSingleton<ViewThrottle>();
        builder.Services.AddTransient<ViewLogger>();

        // Singleton, never scoped (S6): core's singleton ServerEvents authorizers consume IAuthorizationService.
        builder.Services.AddSingleton<IAuthorizationHandler, UserActivityAuthorizationHandler>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(UserActivityPolicy.Read, policy => policy.AddRequirements(new UserActivityReadRequirement()));
            options.AddPolicy(UserActivityPolicy.SelfOrRead, policy => policy.AddRequirements(new UserActivitySelfOrReadRequirement()));
        });
        builder.AddUserActivityOpenApi();

        builder.AddNotificationHandler<UmbracoApplicationStartingNotification, UserActivityStartupCheck>();
    }

    internal static void AddCoreClaims(IServiceCollection services)
        => services.AddSingleton(sp => CoreClaims.From(() => sp.GetRequiredService<UserActivitySourceCollection>()));
}
