using System.Globalization;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Sources.Forms;

/// <summary>
/// The <c>forms</c> source: Umbraco Forms' one <c>umbracoLog</c> row per form action (<c>entityType</c> <c>Form</c>,
/// the acting user's real id, S2), which it claims so the core source skips them (design 3.6). No reference to
/// Forms: available when <c>Umbraco.Forms.Core</c> is loaded, checked by name. Form names come from Forms'
/// <c>UFForms</c> table; a permanently deleted form keeps its log rows but not its name.
/// </summary>
internal sealed class FormsSource : IUserActivitySource, IClaimsCoreAuditRows
{
    public const string AssemblyName = "Umbraco.Forms.Core";

    private readonly IScopeProvider _scopes;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly Func<string, bool> _isLoaded;
    private bool _installed;

    public FormsSource(IScopeProvider scopes, IOptionsMonitor<UserActivityOptions> options)
        : this(scopes, options, OptionalAssemblyGuard.IsLoaded)
    {
    }

    internal FormsSource(IScopeProvider scopes, IOptionsMonitor<UserActivityOptions> options, Func<string, bool> isLoaded)
    {
        _scopes = scopes;
        _options = options;
        _isLoaded = isLoaded;
    }

    public string Alias => SourceAlias.Forms;

    public string DisplayNameKey => "userActivity_sourceForms";

    public bool IsAvailable => _installed || (_installed = _isLoaded(AssemblyName));

    public ActivitySourceInfo Info { get; } = new(
        [FormsSql.EntityType],
        [Categories.Created, Categories.Saved, Categories.Trashed, Categories.Restored, Categories.Moved, Categories.RolledBack, Categories.Deleted, Categories.Other],
        CanCount: true,
        CanSearch: true,
        RetentionHint: null);

    public IReadOnlyCollection<string> ClaimedAuditEventTypePrefixes { get; } = [];

    /// <summary>Only <c>Form</c> (S2). Folder or data source rows, if Forms logs any, stay with the core source.</summary>
    public IReadOnlyCollection<string> ClaimedLogEntityTypes { get; } = ["Form"];

    public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        if (query.EntityTypes.Count > 0 && !query.EntityTypes.Contains(FormsSql.EntityType))
        {
            return Task.FromResult(new ActivitySourcePage([], 0, false));
        }

        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        ct.ThrowIfCancellationRequested();
        var joinForms = FormsTableExists(scope);
        var filter = new FormsSql.FormsFilter(
            query.FromUtc.UtcDateTime, query.ToUtc.UtcDateTime, query.User?.Id, query.Categories, query.Search, query.Ascending, joinForms);

        List<LogRow> rows = scope.Database.SkipTake<LogRow>(0, query.Limit + 1, FormsSql.Rows(scope.SqlContext, filter, count: false));
        ct.ThrowIfCancellationRequested();
        int? total = CoreTimeouts.ShouldCount(query) ? scope.Database.ExecuteScalar<int>(FormsSql.Rows(scope.SqlContext, filter, count: true)) : null;

        var items = rows.Take(query.Limit).Select(r => Map(r, withDetails: false, joinForms)).ToList();
        return Task.FromResult(new ActivitySourcePage(items, total, rows.Count > query.Limit));
    }

    public Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
    {
        if (!id.StartsWith(CoreLogSource.IdPrefix, StringComparison.Ordinal)
            || !int.TryParse(id.AsSpan(CoreLogSource.IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var logId))
        {
            return Task.FromResult<ActivityEvent?>(null);
        }

        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        var joinForms = FormsTableExists(scope);
        LogRow? row = scope.Database.FirstOrDefault<LogRow>(FormsSql.ById(scope.SqlContext, logId, joinForms));
        return Task.FromResult(row is null ? null : Map(row, withDetails: true, joinForms));
    }

    internal static ActivityEvent Map(LogRow row, bool withDetails, bool formsTableExists = true)
    {
        ActivityActor actor = row.UserId is { } userId
            ? new ActivityActor(ActorKind.User, null, userId, null, null)
            : new ActivityActor(ActorKind.System, null, null, null, null);
        var nodeId = row.NodeId.ToString(CultureInfo.InvariantCulture);

        // Without UFForms we can't tell a deleted form from a live one, so the name is simply unknown.
        ActivityEntity entity = !formsTableExists
            ? new ActivityEntity(FormsSql.EntityType, null, nodeId, null, true)
            : row.NodeKey is { } key
                ? new ActivityEntity(FormsSql.EntityType, key.ToString("D"), nodeId, Text.Cap(row.NodeName, ActivityEvent.MaxSummaryLength), true)
                : new ActivityEntity(FormsSql.EntityType, null, nodeId, $"Deleted form (id {nodeId})", false);

        return new ActivityEvent(
            SourceAlias.Forms,
            CoreLogSource.IdPrefix + row.Id.ToString(CultureInfo.InvariantCulture),
            new DateTimeOffset(DateTime.SpecifyKind(row.Datestamp, DateTimeKind.Utc)),
            actor,
            Category(row.LogHeader, row.Parameters),
            row.LogHeader,
            entity,
            Text.Cap(row.LogComment, ActivityEvent.MaxSummaryLength),
            null,
            withDetails ? Details(row) : null);
    }

    /// <summary>Forms logs trash and restore as <c>Move</c>, told apart by <c>parameters</c> (S2).</summary>
    private static string Category(string header, string? parameters)
        => string.Equals(header, "Move", StringComparison.OrdinalIgnoreCase) && parameters is not null
            ? parameters.ToLowerInvariant() switch
            {
                "trash" => Categories.Trashed,
                "restore" => Categories.Restored,
                _ => Categories.Moved,
            }
            : AuditTypeMap.ToCategory(header);

    private static bool FormsTableExists(IScope scope) => scope.SqlContext.SqlSyntax.DoesTableExist(scope.Database, FormsSql.FormsTable);

    private static Dictionary<string, string> Details(LogRow row)
    {
        var details = new Dictionary<string, string>
        {
            ["logHeader"] = row.LogHeader,
            ["nodeId"] = row.NodeId.ToString(CultureInfo.InvariantCulture),
        };
        Text.AddCapped(details, "comment", row.LogComment);
        Text.AddCapped(details, "parameters", row.Parameters);
        return details;
    }
}
