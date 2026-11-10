using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Sources.Automate;

/// <summary>
/// The <c>automate</c> source: Umbraco Automate's own rows in core's <c>umbracoAudit</c> (eventType
/// <c>umbraco/automate/...</c>), which it claims so the core source skips them (design 3.6). No reference to
/// Automate: it is available when the <c>Umbraco.Automate.Core</c> assembly is loaded, checked by name (design 3.2).
/// Automate writes a row only for steps that change CMS data (S3), so this shows those steps, not every run.
/// </summary>
internal sealed partial class AutomateSource : IUserActivitySource, IClaimsCoreAuditRows
{
    public const string AssemblyName = "Umbraco.Automate.Core";
    public const string EventTypePrefix = "umbraco/automate/";
    public const string EntityType = "automation";

    private readonly IScopeProvider _scopes;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly Func<string, bool> _isLoaded;

    // Assemblies never unload, so once seen it stays available; "not yet" is asked again.
    private bool _installed;

    public AutomateSource(IScopeProvider scopes, IOptionsMonitor<UserActivityOptions> options)
        : this(scopes, options, OptionalAssemblyGuard.IsLoaded)
    {
    }

    internal AutomateSource(IScopeProvider scopes, IOptionsMonitor<UserActivityOptions> options, Func<string, bool> isLoaded)
    {
        _scopes = scopes;
        _options = options;
        _isLoaded = isLoaded;
    }

    public string Alias => SourceAlias.Automate;

    public string DisplayNameKey => "userActivity_sourceAutomate";

    public bool IsAvailable => _installed || (_installed = _isLoaded(AssemblyName));

    public ActivitySourceInfo Info { get; } = new([EntityType], [Categories.Ran], CanCount: true, CanSearch: true, RetentionHint: null);

    public IReadOnlyCollection<string> ClaimedAuditEventTypePrefixes { get; } = [EventTypePrefix];

    public IReadOnlyCollection<string> ClaimedLogEntityTypes { get; } = [];

    public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        if ((query.Categories.Count > 0 && !query.Categories.Contains(Categories.Ran))
            || (query.EntityTypes.Count > 0 && !query.EntityTypes.Contains(EntityType)))
        {
            return Task.FromResult(new ActivitySourcePage([], 0, false));
        }

        using IScope scope = _scopes.CreateScope(autoComplete: true);
        UserActivityOptions options = _options.CurrentValue;
        CoreTimeouts.Apply(scope, options);
        ct.ThrowIfCancellationRequested();
        (int Low, int High)? range = CoreAuditSource.IdRange(scope, query.FromUtc.UtcDateTime, query.ToUtc.UtcDateTime);
        if (range is null)
        {
            return Task.FromResult(new ActivitySourcePage([], 0, false));
        }

        // The person who started a run is only in eventDetails; the quoted value keeps "replay:<key>" out.
        var filter = new CoreAuditSql.ClaimedFilter(
            range.Value.Low,
            range.Value.High,
            query.FromUtc.UtcDateTime,
            query.ToUtc.UtcDateTime,
            EventTypePrefix,
            query.User?.Key,
            query.User?.Id,
            query.User is { } user ? $"\"user:{user.Key:D}\"" : null,
            query.Search,
            query.Ascending);

        ct.ThrowIfCancellationRequested();
        List<AuditRow> rows = scope.Database.SkipTake<AuditRow>(0, query.Limit + 1, CoreAuditSql.Claimed(scope.SqlContext, filter, count: false));
        ct.ThrowIfCancellationRequested();
        int? total = CoreTimeouts.ShouldCount(query) ? scope.Database.ExecuteScalar<int>(CoreAuditSql.Claimed(scope.SqlContext, filter, count: true)) : null;

        var items = rows.Take(query.Limit)
            .Select(r => Map(r, options.ShowIpAddresses, withDetails: false))
            .OfType<ActivityEvent>()
            .ToList();
        return Task.FromResult(new ActivitySourcePage(items, total, rows.Count > query.Limit));
    }

    public Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
    {
        if (!id.StartsWith(CoreAuditSource.IdPrefix, StringComparison.Ordinal)
            || !int.TryParse(id.AsSpan(CoreAuditSource.IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var auditId))
        {
            return Task.FromResult<ActivityEvent?>(null);
        }

        using IScope scope = _scopes.CreateScope(autoComplete: true);
        UserActivityOptions options = _options.CurrentValue;
        CoreTimeouts.Apply(scope, options);
        AuditRow? row = scope.Database.FirstOrDefault<AuditRow>(CoreAuditSql.ClaimedById(scope.SqlContext, auditId, EventTypePrefix));
        return Task.FromResult(row is null ? null : Map(row, options.ShowIpAddresses, withDetails: true));
    }

    /// <summary>One row as an event, or null when its details are unreadable in a way that means skip (too deep).</summary>
    internal static ActivityEvent? Map(AuditRow row, bool showIp, bool withDetails)
    {
        AutomateDetails details = AutomateEventDetailsParser.Parse(row.EventDetails);
        if (details.Status == AutomateDetailsStatus.Skip)
        {
            return null;
        }

        var performer = new ActivityActor(ActorKind.ServiceAccount, row.PerformingUserKey, row.PerformingUserId, null, null);
        ActivityActor actor = details.Status switch
        {
            AutomateDetailsStatus.Ok when details.InitiatorUserKey is { } human => new ActivityActor(ActorKind.User, human, null, null, performer),
            AutomateDetailsStatus.Ok => new ActivityActor(ActorKind.Automation, null, null, AutomationLabel(details.InitiatorKind), performer),
            _ => performer,
        };

        return new ActivityEvent(
            SourceAlias.Automate,
            CoreAuditSource.IdPrefix + row.Id.ToString(CultureInfo.InvariantCulture),
            new DateTimeOffset(DateTime.SpecifyKind(row.EventDateUtc, DateTimeKind.Utc)),
            actor,
            Categories.Ran,
            row.EventType,
            details.Status == AutomateDetailsStatus.Ok && details.AutomationId is { } automation
                ? new ActivityEntity(EntityType, Text.Cap(automation, 100), null, null, true)
                : null,
            Summary(row.EventType),
            showIp ? row.PerformingIp : null,
            withDetails ? Details(row, details) : null);
    }

    /// <summary>"Automate (scheduled)"; a kind that isn't a plain word is not echoed.</summary>
    private static string AutomationLabel(string? kind)
        => kind is not null && KindPattern().IsMatch(kind) ? $"Automate ({kind})" : "Automate";

    /// <summary>"createContent step ran", from the action alias at the end of the event type.</summary>
    private static string Summary(string eventType)
    {
        var action = eventType[(eventType.LastIndexOf('/') + 1)..];
        return Text.Cap(action.Length == 0 ? "Step ran" : $"{action} step ran", ActivityEvent.MaxSummaryLength)!;
    }

    /// <summary>Readers only. Never the IP: <see cref="ActivityEvent.IpAddress"/> carries it under one setting.</summary>
    private static Dictionary<string, string> Details(AuditRow row, AutomateDetails details)
    {
        var result = new Dictionary<string, string>
        {
            ["eventType"] = row.EventType,
            ["performingUserId"] = row.PerformingUserId.ToString(CultureInfo.InvariantCulture),
        };
        Text.AddCapped(result, "performingUserKey", row.PerformingUserKey?.ToString("D"));
        Text.AddCapped(result, "performingDetails", row.PerformingDetails);
        Text.AddCapped(result, "automationId", details.AutomationId);
        Text.AddCapped(result, "runId", details.RunId);
        Text.AddCapped(result, "stepId", details.StepId);
        Text.AddCapped(result, "workspace", details.Workspace);
        Text.AddCapped(result, "initiatedBy", details.InitiatedBy);
        if (details.Status != AutomateDetailsStatus.Ok)
        {
            // The details didn't say who started the run, so the service account is shown as the actor.
            result["actorFallback"] = "performer";
        }

        return result;
    }

    [GeneratedRegex("^[a-z][a-z-]{0,31}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex KindPattern();
}
