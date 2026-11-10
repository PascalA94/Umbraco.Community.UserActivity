using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.Query;

/// <summary>
/// Resolves the user once, asks each active source for its top <c>skip + take</c> rows in the window, merges them
/// in <see cref="ActivityEventOrder"/> and returns the page (design 3.7). Offset paging with a depth cap; sources
/// run one after another (Umbraco scopes are ambient per async flow, so parallel scopes are a trap) with a timeout
/// each; a failing source is reported, not fatal.
/// </summary>
public sealed class ActivityQueryEngine
{
    private readonly SourceAvailability _availability;
    private readonly UserIdentityResolver _resolver;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<ActivityQueryEngine> _logger;
    private readonly SourceFailures _failures;

    public ActivityQueryEngine(
        SourceAvailability availability,
        UserIdentityResolver resolver,
        IOptionsMonitor<UserActivityOptions> options,
        TimeProvider time,
        ILogger<ActivityQueryEngine> logger,
        SourceFailures failures)
    {
        _availability = availability;
        _resolver = resolver;
        _options = options;
        _time = time;
        _logger = logger;
        _failures = failures;
    }

    public async Task<ActivityPage> QueryAsync(ActivityQuery query, CancellationToken ct)
    {
        UserActivityOptions options = _options.CurrentValue;
        ResolvedUser? user = null;
        if (query.UserKey is { } key)
        {
            user = await _resolver.ResolveFilterAsync(key, ct);
            if (user is null)
            {
                return ActivityPage.NotFound;
            }
        }

        var sources = _availability.GetActive()
            .Where(s => query.Sources.Count == 0 || query.Sources.Contains(s.Alias))
            .ToList();

        // Depth cap (design 3.7): pages past MaxDepth are never fetched, which bounds every source at MaxDepth + take rows.
        if (query.Skip >= options.MaxDepth)
        {
            // Nothing ran, so no source status is reported.
            return new ActivityPage([], null, false, true, [], new Dictionary<ActorRef, ActorDisplay>());
        }

        var limit = query.Skip + query.Take;
        var sourceQuery = new ActivitySourceQuery(user, query.Categories, query.EntityTypes, query.Window.FromUtc, query.Window.ToUtc, query.Search, limit, query.Ascending);

        var results = new List<SourceResult>();
        var collected = new List<ActivityEvent>();
        var truncated = false;
        var lowerBound = 0;
        foreach (IUserActivitySource source in sources)
        {
            // A source that can't search would return unfiltered rows as if they matched.
            if (query.Search is not null && !source.Info.CanSearch)
            {
                results.Add(new SourceResult(source.Alias, SourceStatus.Skipped, null));
                continue;
            }

            if (_failures.IsBroken(source.Alias))
            {
                results.Add(new SourceResult(source.Alias, SourceStatus.Failed, null));
                continue;
            }

            (ActivitySourcePage? page, string status) = await RunAsync(source, sourceQuery, options, ct);
            results.Add(new SourceResult(source.Alias, status, page?.Total));
            if (page is null)
            {
                continue;
            }

            truncated |= page.Truncated;
            lowerBound += page.Total ?? page.Items.Count;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            collected.AddRange(page.Items.Where(e => seen.Add(e.Id) && (user is null || Involves(e.Actor, user))));
        }

        var merged = collected.Order(query.Ascending ? ActivityEventOrder.Ascending : ActivityEventOrder.Descending).ToList();
        var items = merged.Skip(query.Skip).Take(query.Take).Select(e => GateIp(e, options)).ToList();
        var exact = results.All(r => (r.Status == SourceStatus.Ok && r.Count is not null) || r.Status == SourceStatus.Skipped);
        // Inexact totals are a lower bound ("1,000+"): counts where sources gave them, rows seen where they didn't.
        var depthCapped = limit >= options.MaxDepth && (truncated || merged.Count > limit);

        return new ActivityPage(items, lowerBound, exact, depthCapped, results, await ResolveActorsAsync(items, ct));
    }

    /// <summary>One event from an active source, or null (unknown, inactive, missing, or the source failed).</summary>
    public async Task<ActivityDetail?> GetAsync(string sourceAlias, string id, CancellationToken ct)
    {
        IUserActivitySource? source = _availability.GetActive(sourceAlias);
        if (source is null || _failures.IsBroken(source.Alias))
        {
            return null;
        }

        ActivityEvent? found;
        try
        {
            found = await source.GetAsync(id, ct).WaitAsync(TimeSpan.FromSeconds(_options.CurrentValue.SourceTimeoutSeconds), _time, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogFailure(source, ex);
            return null;
        }

        return found is null || !string.Equals(found.Source, source.Alias, StringComparison.Ordinal)
            ? null
            : new ActivityDetail(GateIp(found, _options.CurrentValue), await ResolveActorsAsync([found], ct));
    }

    /// <summary>
    /// <c>ShowIpAddresses</c> (default off, Pascal P8) is enforced here for every source, so a third-party source
    /// that fills <see cref="ActivityEvent.IpAddress"/> can't show it with the setting off (M3 review QA-3).
    /// </summary>
    private static ActivityEvent GateIp(ActivityEvent e, UserActivityOptions options)
        => options.ShowIpAddresses || e.IpAddress is null ? e : e with { IpAddress = null };

    /// <summary>
    /// True when the user is the actor or the <c>Via</c> (design 3.5). Sources filter by user themselves; this
    /// re-check means a source that ignores the filter can't put someone else's row on a user's tab.
    /// </summary>
    public static bool Involves(ActivityActor actor, ResolvedUser user) => Matches(actor, user) || (actor.Via is { } via && Matches(via, user));

    private static bool Matches(ActivityActor actor, ResolvedUser user)
    {
        if (actor.UserKey is { } key)
        {
            return key == user.Key;
        }

        if (actor.UserId is { } id)
        {
            return id == user.Id;
        }

        // Username-only sources (uSync) keep the username as the stored name. An automation's or the system's stored
        // name is a label ("Automate (scheduled)"), never a username.
        return actor.Kind is not (ActorKind.Automation or ActorKind.System) && actor.StoredName is { } name && user.Username is { } username && string.Equals(name, username, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(ActivitySourcePage? Page, string Status)> RunAsync(IUserActivitySource source, ActivitySourceQuery query, UserActivityOptions options, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var limit = TimeSpan.FromSeconds(options.SourceTimeoutSeconds);
        long started = _time.GetTimestamp();
        try
        {
            ActivitySourcePage page = await source.QueryAsync(query, timeout.Token)
                .WaitAsync(TimeSpan.FromSeconds(options.SourceTimeoutSeconds), _time, ct);
            return (page, SourceStatus.Ok);
        }
        catch (TimeoutException)
        {
            await timeout.CancelAsync();
            _logger.LogWarning("User Activity source {SourceAlias} timed out after {TimeoutSeconds} s.", source.Alias, options.SourceTimeoutSeconds);
            return (null, SourceStatus.TimedOut);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && !SourceFailures.IsBindingFailure(ex) && _time.GetElapsedTime(started) >= limit)
        {
            // Synchronous sources (core's SQL) can't be preempted; their own command timeout throws at the limit.
            _logger.LogWarning("User Activity source {SourceAlias} timed out after {TimeoutSeconds} s ({ExceptionType}).", source.Alias, options.SourceTimeoutSeconds, ex.GetType().FullName);
            return (null, SourceStatus.TimedOut);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            LogFailure(source, ex);
            return (null, SourceStatus.Failed);
        }
    }

    // Exception type only: messages and stack data can carry names, search text or stored values (design 7.4).
    // A binding failure means an optional integration changed shape: that source stays failed until restart
    // (design 3.2), logged once with the integration's version so the site owner knows which upgrade broke it.
    private void LogFailure(IUserActivitySource source, Exception ex)
    {
        if (!SourceFailures.IsBindingFailure(ex))
        {
            _logger.LogWarning("User Activity source {SourceAlias} failed: {ExceptionType}.", source.Alias, ex.GetType().FullName);
            return;
        }

        if (_failures.MarkBroken(source.Alias))
        {
            _logger.LogWarning(
                "User Activity source {SourceAlias} is unavailable until restart: {ExceptionType} (integration assembly version {AssemblyVersion}).",
                source.Alias, ex.GetType().FullName, IntegrationVersion(source));
        }
    }

    private static string? IntegrationVersion(IUserActivitySource source)
    {
        try
        {
            return (source as IRuntimeAdapter)?.IntegrationVersion;
        }
        catch (Exception ex) when (SourceFailures.IsBindingFailure(ex))
        {
            return null;
        }
    }

    private async Task<IReadOnlyDictionary<ActorRef, ActorDisplay>> ResolveActorsAsync(IEnumerable<ActivityEvent> items, CancellationToken ct)
    {
        var refs = items
            .SelectMany(e => new[] { e.Actor, e.Actor.Via })
            .Select(a => a is null ? null : ActorRef.For(a))
            .OfType<ActorRef>()
            .ToList();
        return refs.Count == 0 ? new Dictionary<ActorRef, ActorDisplay>() : await _resolver.ResolveManyAsync(refs, ct);
    }
}
