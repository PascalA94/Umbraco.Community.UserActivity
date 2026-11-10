using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.UserActivity.Sources;

/// <summary>
/// Builds the source collection at boot, so a duplicate alias or an overlapping claim fails start-up rather than
/// the first request (design 3.6), and warns once per unknown <c>Sources</c> setting.
/// </summary>
public sealed class UserActivityStartupCheck : INotificationHandler<UmbracoApplicationStartingNotification>
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SourceAvailability> _logger;

    public UserActivityStartupCheck(IServiceProvider services, ILogger<SourceAvailability> logger)
    {
        _services = services;
        _logger = logger;
    }

    public void Handle(UmbracoApplicationStartingNotification notification)
    {
        SourceAvailability availability = _services.GetRequiredService<SourceAvailability>();
        _ = availability.All.Count();
        availability.LogUnknownSettings(_logger);
    }
}
