using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;

namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// Reads core's <c>umbracoLog</c> (content, media, member, type and other entity events, keyed by user id).
/// One half of the <c>core</c> source; <see cref="CoreSource"/> merges it with <c>umbracoAudit</c>.
/// </summary>
internal sealed class CoreLogSource
{
    public const string IdPrefix = "log:";

    private readonly IScopeProvider _scopes;
    private readonly CoreClaims _claims;
    private readonly IOptionsMonitor<UserActivityOptions> _options;

    public CoreLogSource(IScopeProvider scopes, CoreClaims claims, IOptionsMonitor<UserActivityOptions> options)
    {
        _scopes = scopes;
        _claims = claims;
        _options = options;
    }

    public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        (IReadOnlyCollection<string>? headers, bool includeOther, bool none) = HeaderFilter(query.Categories);
        if (none)
        {
            return Task.FromResult(new ActivitySourcePage([], 0, false));
        }

        var filter = new CoreSql.LogFilter(
            query.FromUtc.UtcDateTime,
            query.ToUtc.UtcDateTime,
            query.User?.Id,
            headers,
            includeOther,
            query.EntityTypes.Select(e => e.ToLowerInvariant()).ToList(),
            _claims.LogEntityTypes,
            query.Search,
            query.Ascending);

        // Read-only: autoComplete only ends the scope, nothing is written (design 3.8).
        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        ct.ThrowIfCancellationRequested();
        List<LogRow> rows = scope.Database.SkipTake<LogRow>(0, query.Limit + 1, CoreSql.Log(scope.SqlContext, filter, count: false));
        ct.ThrowIfCancellationRequested();
        int? total = CoreTimeouts.ShouldCount(query) ? scope.Database.ExecuteScalar<int>(CoreSql.Log(scope.SqlContext, filter, count: true)) : null;

        var items = rows.Take(query.Limit).Select(r => Map(r, withDetails: false)).ToList();
        return Task.FromResult(new ActivitySourcePage(items, total, rows.Count > query.Limit));
    }

    public Task<ActivityEvent?> GetAsync(int id, CancellationToken ct)
    {
        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        LogRow? row = scope.Database.FirstOrDefault<LogRow>(CoreSql.LogById(scope.SqlContext, id, _claims.LogEntityTypes));
        return Task.FromResult(row is null ? null : Map(row, withDetails: true));
    }

    /// <summary>Header filter for the requested categories; <c>none</c> when no core header can match.</summary>
    private static (IReadOnlyCollection<string>? Headers, bool IncludeOther, bool None) HeaderFilter(IReadOnlySet<string> categories)
    {
        if (categories.Count == 0)
        {
            return (null, false, false);
        }

        var headers = categories.SelectMany(AuditTypeMap.HeadersFor).Distinct().ToList();
        var includeOther = categories.Contains(Categories.Other);
        return (headers, includeOther, headers.Count == 0 && !includeOther);
    }

    private static ActivityEvent Map(LogRow row, bool withDetails)
    {
        ActivityActor actor = row.UserId is { } userId
            ? new ActivityActor(ActorKind.User, null, userId, null, null)
            : new ActivityActor(ActorKind.System, null, null, null, null);

        return new ActivityEvent(
            SourceAlias.Core,
            IdPrefix + row.Id,
            new DateTimeOffset(DateTime.SpecifyKind(row.Datestamp, DateTimeKind.Utc)),
            actor,
            AuditTypeMap.ToCategory(row.LogHeader),
            row.LogHeader,
            MapEntity(row),
            Text.Cap(row.LogComment, ActivityEvent.MaxSummaryLength),
            null,
            withDetails ? Details(row) : null);
    }

    private static ActivityEntity? MapEntity(LogRow row)
    {
        if (row.EntityType is null && row.NodeId == 0)
        {
            return null;
        }

        var type = Text.CamelCase(row.EntityType) ?? "unknown";
        var id = row.NodeId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Stylesheets, scripts and partial views are logged against -1 with no file name (design 0, core gap).
        if (row.NodeId == Constants.System.Root)
        {
            return new ActivityEntity(type, null, id, "File (name not recorded by Umbraco)", false);
        }

        // umbracoLog.NodeId means different things per entity type (a user id for User, a language id for Language).
        // The join is only made for node-backed types, and then trusted only when the node's object type matches.
        if (!CoreSql.NodeBackedEntityTypes.Contains(row.EntityType?.ToLowerInvariant())
            || !Enum.TryParse(row.EntityType, ignoreCase: true, out UmbracoObjectTypes objectType))
        {
            return new ActivityEntity(type, null, id, null, true);
        }

        return row.NodeObjectType == ObjectTypes.GetGuid(objectType)
            ? new ActivityEntity(type, row.NodeKey?.ToString("D"), id, row.NodeName, true)
            : new ActivityEntity(type, null, id, $"Deleted item (id {id})", false);
    }

    private static Dictionary<string, string> Details(LogRow row)
    {
        var details = new Dictionary<string, string>
        {
            ["logHeader"] = row.LogHeader,
            ["nodeId"] = row.NodeId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        Text.AddCapped(details, "entityType", row.EntityType);
        Text.AddCapped(details, "comment", row.LogComment);
        Text.AddCapped(details, "parameters", row.Parameters);
        return details;
    }
}
