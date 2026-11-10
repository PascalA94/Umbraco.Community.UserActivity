using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;

namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>
/// Daily clean-up of recorded events older than <c>UserActivity:Recorder:RetentionDays</c> (design 4.2). A distributed
/// job (the family <c>LogScrubberJob</c> belongs to), so on a load-balanced site exactly one server runs it. With 0 it
/// does nothing: the default keeps rows forever, so the package never deletes what an owner has not chosen to delete.
/// </summary>
internal sealed class RetentionJob : IDistributedBackgroundJob
{
    public const int BatchSize = 1000;

    private readonly IScopeProvider _scopes;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<RetentionJob> _logger;

    public RetentionJob(IScopeProvider scopes, IOptionsMonitor<UserActivityOptions> options, TimeProvider time, ILogger<RetentionJob> logger)
    {
        _scopes = scopes;
        _options = options;
        _time = time;
        _logger = logger;
    }

    public string Name => "UserActivityRecorderRetention";

    public TimeSpan Period => TimeSpan.FromDays(1);

    public Task ExecuteAsync()
    {
        var days = _options.CurrentValue.Recorder.RetentionDays;
        if (days is <= 0 or > UserActivityOptionsValidator.RetentionDaysCeiling)
        {
            return Task.CompletedTask;
        }

        try
        {
            PurgeResult result = Purge(_time.GetUtcNow().UtcDateTime.AddDays(-days), CancellationToken.None);
            _logger.LogInformation("Recorder retention removed {Count} events older than {RetentionDays} days.", result.Deleted, days);
        }
        catch (Exception ex)
        {
            // A job that throws is the host's problem to report; ours is to say what failed without the message.
            _logger.LogWarning("Recorder retention could not finish ({ExceptionType}).", ex.GetType().Name);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes in batches of <see cref="BatchSize"/>, each in its own short scope, so a first run over millions of rows
    /// never holds one long transaction or a lock on the table.
    /// </summary>
    internal PurgeResult Purge(DateTime cutoffUtc, CancellationToken ct)
    {
        var deleted = 0;
        var batches = 0;
        while (!ct.IsCancellationRequested)
        {
            using IScope scope = _scopes.CreateScope();
            List<long> ids = scope.Database.SkipTake<long>(0, BatchSize, RecorderSql.IdsOlderThan(scope.SqlContext, cutoffUtc));
            if (ids.Count == 0)
            {
                scope.Complete();
                break;
            }

            deleted += scope.Database.Execute(RecorderSql.DeleteByIds(scope.SqlContext, ids));
            scope.Complete();
            batches++;
            if (ids.Count < BatchSize)
            {
                break;
            }
        }

        return new PurgeResult(deleted, batches);
    }
}

internal readonly record struct PurgeResult(int Deleted, int Batches);
