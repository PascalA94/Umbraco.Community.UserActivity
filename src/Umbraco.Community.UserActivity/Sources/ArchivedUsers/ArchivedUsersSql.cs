using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Sources.ArchivedUsers;

/// <summary>
/// SQL over the <c>umbracoLog</c> rows Archived Users writes (entity type <c>User</c>, header <c>Custom</c>, comment
/// <c>ArchivedUsers: ...</c>, S5), read-only. Same rules as <see cref="CoreSql"/>. The <c>Datestamp</c> range comes
/// first so <c>IX_umbracoLog_datestamp</c> bounds the scan.
/// </summary>
internal static class ArchivedUsersSql
{
    /// <param name="Actions">Actions to keep (the word after the prefix), or null for all.</param>
    public sealed record Filter(DateTime FromUtc, DateTime ToUtc, int? UserId, IReadOnlyCollection<string>? Actions, bool IncludeOtherActions, string? Search, bool Ascending);

    public static Sql<ISqlContext> Rows(ISqlContext ctx, ClaimedLogRows claim, Filter filter, bool count)
    {
        Names n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(count ? "SELECT COUNT(*)" : Columns(n)).Append($"FROM {n.Table} l");
        sql.Append($"WHERE l.{n.Datestamp} >= @0 AND l.{n.Datestamp} <= @1", filter.FromUtc, filter.ToUtc);
        AppendClaim(sql, n, claim);

        if (filter.UserId is { } userId)
        {
            sql.Append($"AND l.{n.UserId} = @0", userId);
        }

        if (filter.Actions is { } actions)
        {
            var known = ArchivedUsersSource.CategoryByAction.Keys.Select(a => CoreSql.PrefixPattern(claim.CommentPrefix + " " + a + " ")).ToList();
            var wanted = actions.Select(a => CoreSql.PrefixPattern(claim.CommentPrefix + " " + a + " ")).ToList();
            sql.Append("AND (1 = 0");
            foreach (var pattern in wanted)
            {
                sql.Append($"OR l.{n.Comment} LIKE @0 ESCAPE '{CoreSql.LikeEscape}'", pattern);
            }

            if (filter.IncludeOtherActions)
            {
                sql.Append("OR (1 = 1");
                foreach (var pattern in known)
                {
                    sql.Append($"AND l.{n.Comment} NOT LIKE @0 ESCAPE '{CoreSql.LikeEscape}'", pattern);
                }

                sql.Append(")");
            }

            sql.Append(")");
        }

        if (filter.Search is { } search)
        {
            sql.Append($"AND l.{n.Comment} LIKE @0 ESCAPE '{CoreSql.LikeEscape}'", CoreSql.ContainsPattern(search));
        }

        if (!count)
        {
            var direction = filter.Ascending ? "ASC" : "DESC";
            sql.Append($"ORDER BY l.{n.Datestamp} {direction}, l.{n.Id} {direction}");
        }

        return sql;
    }

    public static Sql<ISqlContext> ById(ISqlContext ctx, ClaimedLogRows claim, int id)
    {
        Names n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(Columns(n)).Append($"FROM {n.Table} l").Append($"WHERE l.{n.Id} = @0", id);
        AppendClaim(sql, n, claim);
        return sql;
    }

    private static void AppendClaim(Sql<ISqlContext> sql, Names n, ClaimedLogRows claim)
        => sql.Append(
            $"AND LOWER(l.{n.EntityType}) = @0 AND LOWER(l.{n.Header}) = @1 AND l.{n.Comment} LIKE @2 ESCAPE '{CoreSql.LikeEscape}'",
            claim.EntityType.ToLowerInvariant(), claim.LogHeader.ToLowerInvariant(), CoreSql.PrefixPattern(claim.CommentPrefix));

    private static string Columns(Names n)
        => $"SELECT l.{n.Id} AS Id, l.{n.UserId} AS UserId, l.{n.NodeId} AS NodeId, l.{n.EntityType} AS EntityType, " +
           $"l.{n.Datestamp} AS Datestamp, l.{n.Header} AS LogHeader, l.{n.Comment} AS LogComment, l.{n.Parameters} AS Parameters";

    private sealed class Names(ISqlSyntaxProvider s)
    {
        public string Table { get; } = s.GetQuotedTableName("umbracoLog");
        public string Id { get; } = s.GetQuotedColumnName("id");
        public string UserId { get; } = s.GetQuotedColumnName("userId");
        public string NodeId { get; } = s.GetQuotedColumnName("NodeId");
        public string EntityType { get; } = s.GetQuotedColumnName("entityType");
        public string Datestamp { get; } = s.GetQuotedColumnName("Datestamp");
        public string Header { get; } = s.GetQuotedColumnName("logHeader");
        public string Comment { get; } = s.GetQuotedColumnName("logComment");
        public string Parameters { get; } = s.GetQuotedColumnName("parameters");
    }
}
