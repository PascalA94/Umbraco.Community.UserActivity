using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;

namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// SQL over core's <c>umbracoLog</c> (and, in <c>CoreAuditSql</c>, <c>umbracoAudit</c>), read-only (design 3.8).
/// Core's DTOs are internal on both lines (S5), so table and column names are constants quoted by the site's
/// <see cref="ISqlSyntaxProvider"/>. Every value is a parameter, sort is a bool, columns are listed (never
/// <c>SELECT *</c>, so a new core column can't leak into the API), and search is <c>LIKE ... ESCAPE '\'</c>.
/// </summary>
internal static class CoreSql
{
    public const string LikeEscape = "\\";

    /// <param name="Headers">Lower-cased <c>logHeader</c> values to keep, or null for every header.</param>
    /// <param name="IncludeOtherHeaders">Also keep headers outside <see cref="AuditTypeMap.NonOtherHeaders"/> (category <c>other</c>).</param>
    /// <param name="EntityTypes">Lower-cased <c>entityType</c> values to keep; empty for all.</param>
    /// <param name="ExcludedEntityTypes">Lower-cased claimed <c>entityType</c> values to skip.</param>
    /// <param name="Search">Raw search text; escaped here.</param>
    public sealed record LogFilter(
        DateTime FromUtc,
        DateTime ToUtc,
        int? UserId,
        IReadOnlyCollection<string>? Headers,
        bool IncludeOtherHeaders,
        IReadOnlyCollection<string> EntityTypes,
        IReadOnlyCollection<string> ExcludedEntityTypes,
        string? Search,
        bool Ascending);

    /// <summary>
    /// Entity types whose <c>umbracoLog.NodeId</c> is an <c>umbracoNode</c> id (lower case). For every other type
    /// (User, Language, RelationType, ...) NodeId means something else, so the node join is never made for them:
    /// their rows can't borrow a node's name, in the view or in search (M1 review QA-4, QA-5).
    /// </summary>
    public static IReadOnlyList<string> NodeBackedEntityTypes { get; } =
    [
        "document", "media", "member", "documenttype", "mediatype", "membertype", "membergroup", "datatype", "template",
        "documentblueprint", "element", "documenttypecontainer", "mediatypecontainer", "membertypecontainer",
        "datatypecontainer", "documentblueprintcontainer", "elementcontainer",
    ];

    public static Sql<ISqlContext> Log(ISqlContext ctx, LogFilter filter, bool count)
    {
        LogNames n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(count ? "SELECT COUNT(*)" : LogColumns(n)).Append(LogFrom(n), NodeBackedEntityTypes);

        sql.Append($"WHERE l.{n.Datestamp} >= @0 AND l.{n.Datestamp} <= @1", filter.FromUtc, filter.ToUtc);

        if (filter.UserId is { } userId)
        {
            sql.Append($"AND l.{n.UserId} = @0", userId);
        }

        AppendHeaderFilter(sql, n, filter);

        if (filter.EntityTypes.Count > 0)
        {
            sql.Append($"AND LOWER(l.{n.EntityType}) IN (@0)", filter.EntityTypes);
        }

        AppendExclusion(sql, n, filter.ExcludedEntityTypes);

        if (filter.Search is { } search)
        {
            sql.Append(
                $"AND (l.{n.Comment} LIKE @0 ESCAPE '{LikeEscape}' OR l.{n.Parameters} LIKE @0 ESCAPE '{LikeEscape}' OR n.{n.NodeText} LIKE @0 ESCAPE '{LikeEscape}')",
                ContainsPattern(search));
        }

        if (!count)
        {
            var direction = filter.Ascending ? "ASC" : "DESC";
            sql.Append($"ORDER BY l.{n.Datestamp} {direction}, l.{n.Id} {direction}");
        }

        return sql;
    }

    public static Sql<ISqlContext> LogById(ISqlContext ctx, int id, IReadOnlyCollection<string> excludedEntityTypes)
    {
        LogNames n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(LogColumns(n)).Append(LogFrom(n), NodeBackedEntityTypes).Append($"WHERE l.{n.Id} = @0", id);
        AppendExclusion(sql, n, excludedEntityTypes);
        return sql;
    }

    /// <summary><c>%text%</c> with <c>\</c>, <c>%</c>, <c>_</c> and <c>[</c> escaped (design 3.8; <c>[</c> is a wildcard on SQL Server).</summary>
    public static string ContainsPattern(string search)
        => "%" + search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[") + "%";

    private static void AppendHeaderFilter(Sql<ISqlContext> sql, LogNames n, LogFilter filter)
    {
        var headers = filter.Headers;
        if (headers is null)
        {
            return;
        }

        if (headers.Count > 0 && filter.IncludeOtherHeaders)
        {
            sql.Append($"AND (LOWER(l.{n.Header}) IN (@0) OR LOWER(l.{n.Header}) NOT IN (@1))", headers, AuditTypeMap.NonOtherHeaders);
        }
        else if (headers.Count > 0)
        {
            sql.Append($"AND LOWER(l.{n.Header}) IN (@0)", headers);
        }
        else if (filter.IncludeOtherHeaders)
        {
            sql.Append($"AND LOWER(l.{n.Header}) NOT IN (@0)", AuditTypeMap.NonOtherHeaders);
        }
    }

    private static void AppendExclusion(Sql<ISqlContext> sql, LogNames n, IReadOnlyCollection<string> excluded)
    {
        if (excluded.Count > 0)
        {
            sql.Append($"AND (l.{n.EntityType} IS NULL OR LOWER(l.{n.EntityType}) NOT IN (@0))", excluded);
        }
    }

    private static string LogColumns(LogNames n)
        => $"SELECT l.{n.Id} AS Id, l.{n.UserId} AS UserId, l.{n.NodeId} AS NodeId, l.{n.EntityType} AS EntityType, " +
           $"l.{n.Datestamp} AS Datestamp, l.{n.Header} AS LogHeader, l.{n.Comment} AS LogComment, l.{n.Parameters} AS Parameters, " +
           $"n.{n.NodeKey} AS NodeKey, n.{n.NodeText} AS NodeName, n.{n.NodeObjectType} AS NodeObjectType";

    private static string LogFrom(LogNames n)
        => $"FROM {n.LogTable} l LEFT JOIN {n.NodeTable} n ON n.{n.NodeIdColumn} = l.{n.NodeId} AND LOWER(l.{n.EntityType}) IN (@0)";

    private sealed class LogNames(ISqlSyntaxProvider s)
    {
        public string LogTable { get; } = s.GetQuotedTableName("umbracoLog");
        public string NodeTable { get; } = s.GetQuotedTableName("umbracoNode");
        public string Id { get; } = s.GetQuotedColumnName("id");
        public string UserId { get; } = s.GetQuotedColumnName("userId");
        public string NodeId { get; } = s.GetQuotedColumnName("NodeId");
        public string EntityType { get; } = s.GetQuotedColumnName("entityType");
        public string Datestamp { get; } = s.GetQuotedColumnName("Datestamp");
        public string Header { get; } = s.GetQuotedColumnName("logHeader");
        public string Comment { get; } = s.GetQuotedColumnName("logComment");
        public string Parameters { get; } = s.GetQuotedColumnName("parameters");
        public string NodeIdColumn { get; } = s.GetQuotedColumnName("id");
        public string NodeKey { get; } = s.GetQuotedColumnName("uniqueId");
        public string NodeText { get; } = s.GetQuotedColumnName("text");
        public string NodeObjectType { get; } = s.GetQuotedColumnName("nodeObjectType");
    }
}

/// <summary>One <c>umbracoLog</c> row with its node, as selected by <see cref="CoreSql.Log"/>.</summary>
internal sealed class LogRow
{
    public int Id { get; set; }

    public int? UserId { get; set; }

    public int NodeId { get; set; }

    public string? EntityType { get; set; }

    public DateTime Datestamp { get; set; }

    public string LogHeader { get; set; } = string.Empty;

    public string? LogComment { get; set; }

    public string? Parameters { get; set; }

    public Guid? NodeKey { get; set; }

    public string? NodeName { get; set; }

    public Guid? NodeObjectType { get; set; }
}
