using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Sources.Forms;

/// <summary>
/// SQL over Forms' rows in core's <c>umbracoLog</c> (<c>entityType</c> <c>Form</c>, S2), read-only, with the live
/// form's key and name from Forms' <c>UFForms</c> (joined on <c>NodeId</c>, Forms' own int id) when that table
/// exists. Same rules as <see cref="CoreSql"/>: parameters only, listed columns, escaped <c>LIKE</c>.
/// </summary>
internal static class FormsSql
{
    public const string EntityType = "form";
    public const string FormsTable = "UFForms";

    /// <param name="Categories">Empty for every category.</param>
    public sealed record FormsFilter(
        DateTime FromUtc,
        DateTime ToUtc,
        int? UserId,
        IReadOnlyCollection<string> Categories,
        string? Search,
        bool Ascending,
        bool JoinForms);

    public static Sql<ISqlContext> Rows(ISqlContext ctx, FormsFilter filter, bool count)
    {
        Names n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(count ? "SELECT COUNT(*)" : Columns(n, filter.JoinForms)).Append(From(n, filter.JoinForms));
        sql.Append($"WHERE LOWER(l.{n.EntityType}) = @0", EntityType);
        sql.Append($"AND l.{n.Datestamp} >= @0 AND l.{n.Datestamp} <= @1", filter.FromUtc, filter.ToUtc);

        if (filter.UserId is { } userId)
        {
            sql.Append($"AND l.{n.UserId} = @0", userId);
        }

        AppendCategories(sql, n, filter.Categories);

        if (filter.Search is { } search)
        {
            var pattern = CoreSql.ContainsPattern(search);
            var escape = $"ESCAPE '{CoreSql.LikeEscape}'";
            sql.Append(filter.JoinForms
                ? $"AND (l.{n.Comment} LIKE @0 {escape} OR l.{n.Parameters} LIKE @0 {escape} OR f.{n.FormName} LIKE @0 {escape})"
                : $"AND (l.{n.Comment} LIKE @0 {escape} OR l.{n.Parameters} LIKE @0 {escape})", pattern);
        }

        if (!count)
        {
            var direction = filter.Ascending ? "ASC" : "DESC";
            sql.Append($"ORDER BY l.{n.Datestamp} {direction}, l.{n.Id} {direction}");
        }

        return sql;
    }

    public static Sql<ISqlContext> ById(ISqlContext ctx, int id, bool joinForms)
    {
        Names n = new(ctx.SqlSyntax);
        return ctx.Sql(Columns(n, joinForms)).Append(From(n, joinForms))
            .Append($"WHERE l.{n.Id} = @0 AND LOWER(l.{n.EntityType}) = @1", id, EntityType);
    }

    /// <summary>
    /// Forms logs trash and restore as <c>Move</c> with <c>parameters</c> <c>trash</c> / <c>restore</c> (S2), so
    /// those three categories split one header; the rest follow <see cref="AuditTypeMap"/>.
    /// </summary>
    private static void AppendCategories(Sql<ISqlContext> sql, Names n, IReadOnlyCollection<string> categories)
    {
        if (categories.Count == 0)
        {
            return;
        }

        var header = $"LOWER(l.{n.Header})";
        var parameters = $"LOWER(l.{n.Parameters})";
        var clauses = new List<(string Clause, object[] Args)>();
        foreach (var category in categories)
        {
            switch (category)
            {
                case Sources.Categories.Trashed:
                    clauses.Add(($"({header} = @0 AND {parameters} = @1)", ["move", "trash"]));
                    break;
                case Sources.Categories.Restored:
                    clauses.Add(($"({header} = @0 AND {parameters} = @1)", ["move", "restore"]));
                    break;
                case Sources.Categories.Moved:
                    clauses.Add(($"({header} = @0 AND ({parameters} IS NULL OR {parameters} NOT IN (@1)))", ["move", new[] { "trash", "restore" }]));
                    break;
                case Sources.Categories.Other:
                    clauses.Add(($"{header} NOT IN (@0)", [AuditTypeMap.NonOtherHeaders]));
                    break;
                default:
                    var headers = AuditTypeMap.HeadersFor(category).Where(h => h != "move").ToList();
                    if (headers.Count > 0)
                    {
                        clauses.Add(($"{header} IN (@0)", [headers]));
                    }

                    break;
            }
        }

        if (clauses.Count == 0)
        {
            sql.Append("AND 1 = 0");
            return;
        }

        sql.Append("AND (");
        for (var i = 0; i < clauses.Count; i++)
        {
            sql.Append((i == 0 ? string.Empty : "OR ") + clauses[i].Clause, clauses[i].Args);
        }

        sql.Append(")");
    }

    private static string Columns(Names n, bool joinForms)
        => $"SELECT l.{n.Id} AS Id, l.{n.UserId} AS UserId, l.{n.NodeId} AS NodeId, l.{n.EntityType} AS EntityType, " +
           $"l.{n.Datestamp} AS Datestamp, l.{n.Header} AS LogHeader, l.{n.Comment} AS LogComment, l.{n.Parameters} AS Parameters, " +
           (joinForms ? $"f.{n.FormKey} AS NodeKey, f.{n.FormName} AS NodeName" : "NULL AS NodeKey, NULL AS NodeName");

    private static string From(Names n, bool joinForms)
        => joinForms ? $"FROM {n.LogTable} l LEFT JOIN {n.FormTable} f ON f.{n.FormNodeId} = l.{n.NodeId}" : $"FROM {n.LogTable} l";

    private sealed class Names(ISqlSyntaxProvider s)
    {
        public string LogTable { get; } = s.GetQuotedTableName("umbracoLog");
        public string FormTable { get; } = s.GetQuotedTableName(FormsTable);
        public string Id { get; } = s.GetQuotedColumnName("id");
        public string UserId { get; } = s.GetQuotedColumnName("userId");
        public string NodeId { get; } = s.GetQuotedColumnName("NodeId");
        public string EntityType { get; } = s.GetQuotedColumnName("entityType");
        public string Datestamp { get; } = s.GetQuotedColumnName("Datestamp");
        public string Header { get; } = s.GetQuotedColumnName("logHeader");
        public string Comment { get; } = s.GetQuotedColumnName("logComment");
        public string Parameters { get; } = s.GetQuotedColumnName("parameters");
        public string FormKey { get; } = s.GetQuotedColumnName("Key");
        public string FormName { get; } = s.GetQuotedColumnName("Name");
        public string FormNodeId { get; } = s.GetQuotedColumnName("NodeId");
    }
}
