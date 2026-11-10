using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.AI.Core.AuditLog;

namespace Umbraco.Community.UserActivity.Sources.Adapters.AI;

/// <summary>
/// The <c>ai</c> source over Umbraco.AI's <see cref="IAIAuditLogService"/> (design 3.2, 3.5). Registered only by
/// <see cref="AIGuard"/> after its probe passed; compiled against the line's oldest release and bound at runtime to
/// whatever is installed. Prompts, responses and error text are never read: they can hold anything people typed.
/// </summary>
internal sealed class AISource : IUserActivitySource, IRuntimeAdapter
{
    public const string IdPrefix = "ai:";
    public const string BackgroundLabel = "Background (Umbraco.AI)";

    private readonly IServiceScopeFactory _scopes;
    private readonly IServiceProviderIsService _isService;
    private readonly IOptionsMonitor<AIAuditLogOptions> _aiOptions;

    public AISource(IServiceScopeFactory scopes, IServiceProviderIsService isService, IOptionsMonitor<AIAuditLogOptions> aiOptions)
    {
        _scopes = scopes;
        _isService = isService;
        _aiOptions = aiOptions;
    }

    public string Alias => SourceAlias.AI;

    public string DisplayNameKey => "userActivity_sourceAi";

    /// <summary>Umbraco.AI.Core can be present without its services (another package's dependency): then hidden.</summary>
    public bool IsAvailable => _isService.IsService(typeof(IAIAuditLogService));

    /// <summary>Umbraco.AI deletes its audit rows after <c>RetentionDays</c> (default 14); 0 or less keeps them.</summary>
    public ActivitySourceInfo Info => new(
        [],
        [Categories.Ran],
        CanCount: true,
        // Umbraco.AI's SearchText matches model ids and error messages, which we never show, so passing a search would
        // let a reader probe hidden error text (final review SEC-F1). The engine skips this source during a search.
        CanSearch: false,
        RetentionHint: _aiOptions.CurrentValue.RetentionDays > 0 ? TimeSpan.FromDays(_aiOptions.CurrentValue.RetentionDays) : null);

    public string? IntegrationVersion => typeof(IAIAuditLogService).Assembly.GetName().Version?.ToString();

    // Every method that names an Umbraco.AI type is synchronous and awaits nothing: see AdapterTasks for why.
    public async Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        // Umbraco.AI's entity types aren't a fixed list, so an entity-type filter can't be passed on faithfully.
        if ((query.Categories.Count > 0 && !query.Categories.Contains(Categories.Ran)) || query.EntityTypes.Count > 0)
        {
            return new ActivitySourcePage([], 0, false);
        }

        using IServiceScope scope = _scopes.CreateScope();
        LogPage page = await FetchAsync(scope.ServiceProvider, query, skip: 0, ct);
        if (query.Ascending && page.Total > query.Limit)
        {
            // Newest first is Umbraco.AI's order; the oldest N are the last page of it.
            page = await FetchAsync(scope.ServiceProvider, query, page.Total - query.Limit, ct) with { Total = page.Total };
        }

        var items = page.Items
            .Order(query.Ascending ? ActivityEventOrder.Ascending : ActivityEventOrder.Descending)
            .Take(query.Limit)
            .ToList();
        return new ActivitySourcePage(items, page.Total, page.Total > query.Limit);
    }

    public async Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
    {
        if (!id.StartsWith(IdPrefix, StringComparison.Ordinal) || !Guid.TryParseExact(id.AsSpan(IdPrefix.Length), "D", out Guid key))
        {
            return null;
        }

        using IServiceScope scope = _scopes.CreateScope();
        return await FetchOneAsync(scope.ServiceProvider, key, ct);
    }

    private static Task<LogPage> FetchAsync(IServiceProvider services, ActivitySourceQuery query, int skip, CancellationToken ct)
    {
        var filter = new AIAuditLogFilter
        {
            // Umbraco.AI stores the user key as a string (design 3.5).
            UserId = query.User?.Key.ToString("D"),
            FromDate = query.FromUtc.UtcDateTime,
            ToDate = query.ToUtc.UtcDateTime,
        };

        return AdapterTasks.Map(
            services.GetRequiredService<IAIAuditLogService>().GetAuditLogsPagedAsync(filter, skip, query.Limit, ct),
            ToPage);
    }

    private static LogPage ToPage((IEnumerable<AIAuditLog> Logs, int Total) result)
        => new(result.Logs.Select(l => Map(l, withDetails: false)).ToList(), result.Total);

    private static Task<ActivityEvent?> FetchOneAsync(IServiceProvider services, Guid key, CancellationToken ct)
        => AdapterTasks.Map(services.GetRequiredService<IAIAuditLogService>().GetAuditLogAsync(key, ct), MapOne);

    private static ActivityEvent? MapOne(AIAuditLog? log) => log is null ? null : Map(log, withDetails: true);

    internal static ActivityEvent Map(AIAuditLog log, bool withDetails)
    {
        var feature = Text.Cap(log.FeatureType, 100);
        return new ActivityEvent(
            SourceAlias.AI,
            IdPrefix + log.Id.ToString("D"),
            new DateTimeOffset(DateTime.SpecifyKind(log.StartTime, DateTimeKind.Utc)),
            Actor(log),
            Categories.Ran,
            feature ?? "ai",
            Entity(log),
            $"{feature ?? "AI request"} ({log.Status})",
            null,
            withDetails ? Details(log) : null);
    }

    private static ActivityActor Actor(AIAuditLog log)
    {
        if (string.IsNullOrEmpty(log.UserId))
        {
            return new ActivityActor(ActorKind.System, null, null, BackgroundLabel, null);
        }

        // Only a key names someone; any other stored value is shown as an unknown user, never matched by name.
        return Guid.TryParse(log.UserId, CultureInfo.InvariantCulture, out Guid key)
            ? new ActivityActor(ActorKind.User, key, null, Text.Cap(log.UserName, 200), null)
            : new ActivityActor(ActorKind.Unknown, null, null, null, null);
    }

    private static ActivityEntity? Entity(AIAuditLog log)
    {
        if (string.IsNullOrEmpty(log.EntityType))
        {
            return null;
        }

        var id = Text.Cap(log.EntityId, 100);
        var key = Guid.TryParse(id, CultureInfo.InvariantCulture, out Guid parsed) ? parsed.ToString("D") : null;
        return new ActivityEntity(Text.CamelCase(Text.Cap(log.EntityType, 100))!, key, id, null, true);
    }

    private static Dictionary<string, string> Details(AIAuditLog log)
    {
        var details = new Dictionary<string, string> { ["status"] = log.Status.ToString() };
        Text.AddCapped(details, "featureType", log.FeatureType);
        Text.AddCapped(details, "profileAlias", log.ProfileAlias);
        Text.AddCapped(details, "providerId", log.ProviderId);
        Text.AddCapped(details, "modelId", log.ModelId);
        Text.AddCapped(details, "entityType", log.EntityType);
        Text.AddCapped(details, "entityId", log.EntityId);
        Text.AddCapped(details, "userId", log.UserId);
        Text.AddCapped(details, "userName", log.UserName);
        return details;
    }

    private sealed record LogPage(IReadOnlyList<ActivityEvent> Items, int Total);
}
