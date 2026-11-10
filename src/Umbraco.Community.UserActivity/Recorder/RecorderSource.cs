using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>
/// The <c>recorder</c> source (design 4): rows the recorder handlers stored in our own table.
/// <para>
/// Availability follows the rule every other source follows, "a source whose package is not installed is hidden":
/// here the "package" is any recorded package, so the source is available only when at least one handler registered
/// (<see cref="RecorderHandlerRegistry"/>). Without one it would be a permanently empty tab and filter entry. The table
/// and its migration are not tied to this: they exist either way, so installing CSP Manager later needs no migration
/// run and loses no rows.
/// </para>
/// </summary>
internal sealed class RecorderSource : IUserActivitySource
{
    public const string IdPrefix = "rec:";

    /// <summary>Entity types the handlers write (design 4.1).</summary>
    internal static IReadOnlyList<string> EntityTypes { get; } = ["cspPolicy", "workflowConfig", "workflowInstance", "redirect"];

    private readonly IScopeProvider _scopes;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly RecorderHandlerRegistry _handlers;

    public RecorderSource(IScopeProvider scopes, IOptionsMonitor<UserActivityOptions> options, RecorderHandlerRegistry handlers)
    {
        _scopes = scopes;
        _options = options;
        _handlers = handlers;
    }

    public string Alias => SourceAlias.Recorder;

    public string DisplayNameKey => "userActivity_sourceRecorder";

    public bool IsAvailable => _handlers.Any;

    public ActivitySourceInfo Info
    {
        get
        {
            // The validator caps the value, but an unvalidated or hot-reloaded one must never make every Activity request throw.
            var days = _options.CurrentValue.Recorder.RetentionDays;
            return new ActivitySourceInfo(
                EntityTypes, [Categories.Created, Categories.Saved, Categories.Deleted, Categories.Other], CanCount: true, CanSearch: true,
                RetentionHint: days is > 0 and <= UserActivityOptionsValidator.RetentionDaysCeiling ? TimeSpan.FromDays(days) : null)
            {
                KeptForever = days == 0,
            };
        }
    }

    public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        var filter = new RecorderSql.Filter(
            query.FromUtc.UtcDateTime, query.ToUtc.UtcDateTime, query.User?.Key, query.Categories, query.EntityTypes, query.Search, query.Ascending);
        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        ct.ThrowIfCancellationRequested();
        List<ActivityEventDto> rows = scope.Database.SkipTake<ActivityEventDto>(0, query.Limit + 1, RecorderSql.Rows(scope.SqlContext, filter, count: false));
        ct.ThrowIfCancellationRequested();
        int? total = CoreTimeouts.ShouldCount(query) ? scope.Database.ExecuteScalar<int>(RecorderSql.Rows(scope.SqlContext, filter, count: true)) : null;

        var items = rows.Take(query.Limit).Select(r => Map(r, withDetails: false)).ToList();
        return Task.FromResult(new ActivitySourcePage(items, total, rows.Count > query.Limit));
    }

    public Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
    {
        if (!id.StartsWith(IdPrefix, StringComparison.Ordinal)
            || !long.TryParse(id.AsSpan(IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var rowId))
        {
            return Task.FromResult<ActivityEvent?>(null);
        }

        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        ActivityEventDto? row = scope.Database.FirstOrDefault<ActivityEventDto>(RecorderSql.ById(scope.SqlContext, rowId));
        return Task.FromResult(row is null ? null : Map(row, withDetails: true));
    }

    internal static ActivityEvent Map(ActivityEventDto row, bool withDetails)
    {
        // No backoffice user at handler time (background or API) is System; a stored key whose user has since been
        // deleted keeps the name we stored, which the engine uses as the fallback (design 3.5).
        ActivityActor actor = row.UserKey is { } key
            ? new ActivityActor(ActorKind.User, key, null, row.UserName, null)
            : new ActivityActor(ActorKind.System, null, null, null, null);

        return new ActivityEvent(
            SourceAlias.Recorder,
            IdPrefix + row.Id.ToString(CultureInfo.InvariantCulture),
            new DateTimeOffset(DateTime.SpecifyKind(row.OccurredUtc, DateTimeKind.Utc)),
            actor,
            Categories.IsKnown(row.Category) ? row.Category : Categories.Other,
            row.RawType,
            new ActivityEntity(row.EntityType, row.EntityKey, null, row.EntityName, true),
            Text.Cap(row.Source + ": " + row.RawType, ActivityEvent.MaxSummaryLength),
            null,
            withDetails ? Details(row) : null);
    }

    private static Dictionary<string, string> Details(ActivityEventDto row)
    {
        var details = new Dictionary<string, string>(StringComparer.Ordinal);
        Text.AddCapped(details, "package", row.Source);
        if (row.DetailsJson is null)
        {
            return details;
        }

        // Our writer produced this, but the table is plain SQL anyone with database access can edit: take string
        // values only and never let a malformed row break the detail view.
        try
        {
            using JsonDocument document = JsonDocument.Parse(row.DetailsJson);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject().Where(p => p.Value.ValueKind == JsonValueKind.String))
                {
                    Text.AddCapped(details, property.Name, property.Value.GetString());
                }
            }
        }
        catch (JsonException)
        {
        }

        return details;
    }
}
