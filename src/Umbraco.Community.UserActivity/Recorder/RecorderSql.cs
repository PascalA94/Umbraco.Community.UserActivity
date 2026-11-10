using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>
/// SQL over our own table. Same rules as <see cref="CoreSql"/> (design 3.8): every value is a parameter, sort is a
/// bool, columns are listed (never <c>SELECT *</c>) and search is <c>LIKE ... ESCAPE '\'</c> with the wildcards escaped.
/// The table is ours, so reads here are the only reads; writes live in <see cref="RecorderWriter"/> and <see cref="RetentionJob"/>.
/// </summary>
internal static class RecorderSql
{
    /// <param name="Categories">Categories to keep; empty for all.</param>
    /// <param name="EntityTypes">Entity types to keep; empty for all.</param>
    public sealed record Filter(
        DateTime FromUtc,
        DateTime ToUtc,
        Guid? UserKey,
        IReadOnlyCollection<string> Categories,
        IReadOnlyCollection<string> EntityTypes,
        string? Search,
        bool Ascending);

    public static Sql<ISqlContext> Rows(ISqlContext ctx, Filter filter, bool count)
    {
        Names n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(count ? "SELECT COUNT(*)" : Columns(n)).Append($"FROM {n.Table} e");
        sql.Append($"WHERE e.{n.OccurredUtc} >= @0 AND e.{n.OccurredUtc} <= @1", filter.FromUtc, filter.ToUtc);

        if (filter.UserKey is { } userKey)
        {
            sql.Append($"AND e.{n.UserKey} = @0", userKey);
        }

        if (filter.Categories.Count > 0)
        {
            sql.Append($"AND e.{n.Category} IN (@0)", filter.Categories);
        }

        if (filter.EntityTypes.Count > 0)
        {
            sql.Append($"AND e.{n.EntityType} IN (@0)", filter.EntityTypes);
        }

        if (filter.Search is { } search)
        {
            sql.Append(
                $"AND (e.{n.EntityName} LIKE @0 ESCAPE '{CoreSql.LikeEscape}' OR e.{n.EntityKey} LIKE @0 ESCAPE '{CoreSql.LikeEscape}' OR e.{n.RawType} LIKE @0 ESCAPE '{CoreSql.LikeEscape}')",
                CoreSql.ContainsPattern(search));
        }

        if (!count)
        {
            var direction = filter.Ascending ? "ASC" : "DESC";
            sql.Append($"ORDER BY e.{n.OccurredUtc} {direction}, e.{n.Id} {direction}");
        }

        return sql;
    }

    public static Sql<ISqlContext> ById(ISqlContext ctx, long id)
    {
        Names n = new(ctx.SqlSyntax);
        return ctx.Sql(Columns(n)).Append($"FROM {n.Table} e").Append($"WHERE e.{n.Id} = @0", id);
    }

    /// <summary>The ids of the oldest rows before the cut-off, one batch (the retention job deletes exactly these).</summary>
    public static Sql<ISqlContext> IdsOlderThan(ISqlContext ctx, DateTime cutoffUtc)
    {
        Names n = new(ctx.SqlSyntax);
        return ctx.Sql($"SELECT e.{n.Id}").Append($"FROM {n.Table} e").Append($"WHERE e.{n.OccurredUtc} < @0", cutoffUtc).Append($"ORDER BY e.{n.Id} ASC");
    }

    public static Sql<ISqlContext> DeleteByIds(ISqlContext ctx, IReadOnlyCollection<long> ids)
    {
        Names n = new(ctx.SqlSyntax);
        return ctx.Sql($"DELETE FROM {n.Table}").Append($"WHERE {n.Id} IN (@0)", ids);
    }

    private static string Columns(Names n)
        => $"SELECT e.{n.Id} AS Id, e.{n.OccurredUtc} AS OccurredUtc, e.{n.Source} AS Source, e.{n.Category} AS Category, " +
           $"e.{n.RawType} AS RawType, e.{n.UserKey} AS UserKey, e.{n.UserName} AS UserName, e.{n.EntityType} AS EntityType, " +
           $"e.{n.EntityKey} AS EntityKey, e.{n.EntityName} AS EntityName, e.{n.DetailsJson} AS DetailsJson";

    private sealed class Names(ISqlSyntaxProvider s)
    {
        public string Table { get; } = s.GetQuotedTableName(RecorderTable.Name);
        public string Id { get; } = s.GetQuotedColumnName("Id");
        public string OccurredUtc { get; } = s.GetQuotedColumnName("OccurredUtc");
        public string Source { get; } = s.GetQuotedColumnName("Source");
        public string Category { get; } = s.GetQuotedColumnName("Category");
        public string RawType { get; } = s.GetQuotedColumnName("RawType");
        public string UserKey { get; } = s.GetQuotedColumnName("UserKey");
        public string UserName { get; } = s.GetQuotedColumnName("UserName");
        public string EntityType { get; } = s.GetQuotedColumnName("EntityType");
        public string EntityKey { get; } = s.GetQuotedColumnName("EntityKey");
        public string EntityName { get; } = s.GetQuotedColumnName("EntityName");
        public string DetailsJson { get; } = s.GetQuotedColumnName("DetailsJson");
    }
}
