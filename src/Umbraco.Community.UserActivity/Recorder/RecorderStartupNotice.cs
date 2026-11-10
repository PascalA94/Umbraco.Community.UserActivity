using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>
/// With <c>RetentionDays</c> 0 the recorder keeps events forever, and UK GDPR's storage limitation principle says
/// personal data should not be kept longer than needed (design 4.2). So the owner gets one Information line at start-up
/// naming the setting, and only when there is a decision to make: the recorder is active and no limit is set.
/// </summary>
internal sealed class RecorderStartupNotice : INotificationHandler<UmbracoApplicationStartingNotification>
{
    private readonly SourceAvailability _availability;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly ILogger<RecorderStartupNotice> _logger;

    public RecorderStartupNotice(SourceAvailability availability, IOptionsMonitor<UserActivityOptions> options, ILogger<RecorderStartupNotice> logger)
    {
        _availability = availability;
        _options = options;
        _logger = logger;
    }

    public void Handle(UmbracoApplicationStartingNotification notification)
    {
        if (_options.CurrentValue.Recorder.RetentionDays == 0 && _availability.GetActive(SourceAlias.Recorder) is not null)
        {
            _logger.LogInformation(
                "The recorder keeps events forever because UserActivity:Recorder:RetentionDays is 0. Set it to a number of days to match your retention policy.");
        }
    }
}
