using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Recorder.Handlers;

/// <summary>
/// What every recorder handler shares (design 4.1): who did it, and the rule that a handler never throws into the
/// other package's save. The handlers run inside CSP Manager's, Workflow's, UrlTracker's or Skybrud's own operation,
/// so a bug or a full disk here must cost us a row, never their save.
/// </summary>
internal sealed class RecorderHandlerContext
{
    private readonly IRecorderWriter _writer;
    private readonly IBackOfficeSecurityAccessor _security;
    private readonly ILogger<RecorderHandlerContext> _logger;

    public RecorderHandlerContext(IRecorderWriter writer, IBackOfficeSecurityAccessor security, ILogger<RecorderHandlerContext> logger)
    {
        _writer = writer;
        _security = security;
        _logger = logger;
    }

    /// <summary>
    /// Builds the events from the notification and stores them for the current backoffice user. No user (a scheduled
    /// job, an API call, a background step) stores a null user and the row shows "No backoffice user".
    /// A failure is logged once as a Warning with the source and the exception type only: the message can carry the
    /// values being recorded (design 7.4).
    /// </summary>
    public void Record(string source, Func<IEnumerable<RecorderEntry>> build)
    {
        try
        {
            (Guid? key, string? name) = CurrentUser();
            foreach (RecorderEntry entry in build())
            {
                _writer.Write(entry with { UserKey = key, UserName = name });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Recorder handler for source {SourceAlias} failed ({ExceptionType}).", Text.Cap(source, RecorderTable.SourceMax), ex.GetType().Name);
        }
    }

    private (Guid? Key, string? Name) CurrentUser()
    {
        try
        {
            IUser? user = _security.BackOfficeSecurity?.CurrentUser;
            return user is null ? (null, null) : (user.Key, user.Name);
        }
        catch
        {
            // An accessor that cannot answer (no request, a disposed scope) is the same as no user: the event is still true.
            return (null, null);
        }
    }
}
