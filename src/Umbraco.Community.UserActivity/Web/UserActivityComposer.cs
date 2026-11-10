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
using Umbraco.Community.UserActivity.Security;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Core;
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

        builder.UserActivitySources().Append<CoreSource>();
        builder.Services.AddSingleton(sp => new CoreClaims(() => sp.GetRequiredService<UserActivitySourceCollection>()
            .Where(s => s is not CoreSource && s.IsAvailable)
            .OfType<IClaimsCoreAuditRows>()));
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
}
