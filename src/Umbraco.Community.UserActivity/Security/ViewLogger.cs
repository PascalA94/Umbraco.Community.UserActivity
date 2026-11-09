using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.UserActivity.Configuration;

namespace Umbraco.Community.UserActivity.Security;

/// <summary>
/// "Watching the watchers" (design 7.1, Pascal P7): opening someone's activity and every export writes one core
/// <c>umbracoAudit</c> row through <see cref="IAuditEntryService"/>, with the viewer as performer and the subject as
/// affected user. Views are throttled to one row per viewer and subject per hour. Our own failure to write never
/// fails the request.
/// </summary>
public sealed class ViewLogger
{
    public const string ViewedEventType = "community/useractivity/viewed";
    public const string ExportedEventType = "community/useractivity/exported";

    private static readonly TimeSpan ViewWindow = TimeSpan.FromHours(1);

    private readonly IAuditEntryService _audit;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly TimeProvider _time;
    private readonly ViewThrottle _throttle;
    private readonly ILogger<ViewLogger> _logger;

    public ViewLogger(IAuditEntryService audit, IOptionsMonitor<UserActivityOptions> options, TimeProvider time, ViewThrottle throttle, ILogger<ViewLogger> logger)
    {
        _audit = audit;
        _options = options;
        _time = time;
        _throttle = throttle;
        _logger = logger;
    }

    /// <summary>A view of one person's activity: their tab, a global view filtered to them, or one of their events.</summary>
    public async Task LogViewAsync(Guid viewerKey, Guid subjectKey, CancellationToken ct)
    {
        DateTimeOffset now = _time.GetUtcNow();
        if (_options.CurrentValue.LogViews && _throttle.TryAcquire(viewerKey, subjectKey, now, ViewWindow)
            && !await WriteAsync(viewerKey, subjectKey, now, ViewedEventType, "viewed"))
        {
            _throttle.Release(viewerKey, subjectKey);
        }
    }

    /// <summary>The unfiltered global view: one row with no subject per viewer per hour (it shows everyone).</summary>
    public async Task LogGlobalViewAsync(Guid viewerKey, CancellationToken ct)
    {
        DateTimeOffset now = _time.GetUtcNow();
        if (_options.CurrentValue.LogViews && _throttle.TryAcquire(viewerKey, Guid.Empty, now, ViewWindow)
            && !await WriteAsync(viewerKey, null, now, ViewedEventType, "viewed all users"))
        {
            _throttle.Release(viewerKey, Guid.Empty);
        }
    }

    /// <param name="subjectKey">The user filter of the export, or null for an export across users.</param>
    public async Task LogExportAsync(Guid viewerKey, Guid? subjectKey, int rows, CancellationToken ct)
    {
        if (_options.CurrentValue.LogViews)
        {
            await WriteAsync(viewerKey, subjectKey, _time.GetUtcNow(), ExportedEventType, $"rows={rows}");
        }
    }

    /// <summary>Core requires performer and event details (non-blank); ours are fixed text.</summary>
    public const string PerformingDetails = "User Activity";

    // No names, search text, filters or IP in the row (data minimisation, design 7.1): the keys identify both people,
    // and the count sizes the export.
    private async Task<bool> WriteAsync(Guid viewer, Guid? subject, DateTimeOffset now, string eventType, string details)
    {
        try
        {
            await _audit.WriteAsync(viewer, PerformingDetails, string.Empty, now.UtcDateTime, subject, string.Empty, eventType, details);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("User Activity could not write its {EventType} audit row: {ExceptionType}.", eventType, ex.GetType().FullName);
            return false;
        }
    }
}
