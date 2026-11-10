using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;

namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// SQL over core's <c>umbracoAudit</c>, read-only (design 3.8). The table has no index but its primary key (S5),
/// so every query is bounded by the id range of the date window (Pascal, 2026-11-07), found by PK seeks in
/// <see cref="CoreAuditSource"/>; the exact window is still applied inside it.
/// </summary>
internal static class CoreAuditSql
{
    /// <param name="UserKey">The filter user's key; with <paramref name="UserId"/> for rows that stored no key.</param>
    /// <param name="EventTypes">Exact event types to keep, or null for all.</param>
    /// <param name="EntityTypePrefixes">Event type prefixes to keep; empty for all.</param>
    /// <param name="ExcludedPrefixes">Claimed prefixes to skip.</param>
    public sealed record AuditFilter(
        int LowId,
        int HighId,
        DateTime FromUtc,
        DateTime ToUtc,
        Guid? UserKey,
        int? UserId,
        IReadOnlyCollection<string>? EventTypes,
        bool IncludeOther,
        IReadOnlyCollection<string> EntityTypePrefixes,
        IReadOnlyCollection<string> ExcludedPrefixes,
        string? Search,
        bool Ascending);

    public static Sql<ISqlContext> Audit(ISqlContext ctx, AuditFilter filter, bool count)
    {
        Names n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(count ? "SELECT COUNT(*)" : Columns(n)).Append($"FROM {n.Table} a");
        sql.Append($"WHERE a.{n.Id} >= @0 AND a.{n.Id} <= @1", filter.LowId, filter.HighId);
        sql.Append($"AND a.{n.Date} >= @0 AND a.{n.Date} <= @1", filter.FromUtc, filter.ToUtc);

        if (filter.UserKey is { } key)
        {
            // Sign-in and sign-out rows store the default admin as performer and the person as the affected user
            // (S5); a failed sign-in names nobody, so it is on no user's tab.
            int id = filter.UserId ?? int.MinValue;
            string[] signIn = [AuditEventTypeMap.SignInLogin, AuditEventTypeMap.SignInLogout];
            string[] signInAll = [.. signIn, AuditEventTypeMap.SignInFailed];
            sql.Append(
                $"AND ((a.{n.EventType} NOT IN (@0) AND (a.{n.PerformerKey} = @1 OR (a.{n.PerformerKey} IS NULL AND a.{n.PerformerId} = @2)))" +
                $" OR (a.{n.EventType} IN (@3) AND (a.{n.AffectedKey} = @1 OR (a.{n.AffectedKey} IS NULL AND a.{n.AffectedId} = @2))))",
                signInAll, key, id, signIn);
        }

        if (filter.EventTypes is { } types)
        {
            if (types.Count > 0 && filter.IncludeOther)
            {
                sql.Append($"AND (a.{n.EventType} IN (@0) OR a.{n.EventType} NOT IN (@1))", types, AuditEventTypeMap.NonOtherEventTypes);
            }
            else if (types.Count > 0)
            {
                sql.Append($"AND a.{n.EventType} IN (@0)", types);
            }
            else if (filter.IncludeOther)
            {
                sql.Append($"AND a.{n.EventType} NOT IN (@0)", AuditEventTypeMap.NonOtherEventTypes);
            }
        }

        if (filter.EntityTypePrefixes.Count > 0)
        {
            sql.Append("AND (");
            var first = true;
            foreach (var prefix in filter.EntityTypePrefixes)
            {
                sql.Append($"{(first ? string.Empty : "OR ")}a.{n.EventType} LIKE @0 ESCAPE '{CoreSql.LikeEscape}'", PrefixPattern(prefix));
                first = false;
            }

            sql.Append(")");
        }

        foreach (var prefix in filter.ExcludedPrefixes)
        {
            sql.Append($"AND a.{n.EventType} NOT LIKE @0 ESCAPE '{CoreSql.LikeEscape}'", PrefixPattern(prefix));
        }

        if (filter.Search is { } search)
        {
            sql.Append(
                $"AND (a.{n.EventType} LIKE @0 ESCAPE '{CoreSql.LikeEscape}' OR a.{n.EventDetails} LIKE @0 ESCAPE '{CoreSql.LikeEscape}')",
                CoreSql.ContainsPattern(search));
        }

        if (!count)
        {
            var direction = filter.Ascending ? "ASC" : "DESC";
            sql.Append($"ORDER BY a.{n.Date} {direction}, a.{n.Id} {direction}");
        }

        return sql;
    }

    /// <summary>Rows another source claimed, read by that source (design 3.6): the same table, bound and columns.</summary>
    /// <param name="Prefix">The claimed <c>eventType</c> prefix; only rows under it are returned.</param>
    /// <param name="UserKey">Rows performed by this account, or whose <c>eventDetails</c> contain <paramref name="DetailsUserPattern"/>.</param>
    /// <param name="DetailsUserPattern">A literal the details hold for this user (Automate: <c>"user:&lt;key&gt;"</c>); escaped here.</param>
    public sealed record ClaimedFilter(
        int LowId,
        int HighId,
        DateTime FromUtc,
        DateTime ToUtc,
        string Prefix,
        Guid? UserKey,
        int? UserId,
        string? DetailsUserPattern,
        string? Search,
        bool Ascending);

    public static Sql<ISqlContext> Claimed(ISqlContext ctx, ClaimedFilter filter, bool count)
    {
        Names n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(count ? "SELECT COUNT(*)" : Columns(n)).Append($"FROM {n.Table} a");
        sql.Append($"WHERE a.{n.Id} >= @0 AND a.{n.Id} <= @1", filter.LowId, filter.HighId);
        sql.Append($"AND a.{n.Date} >= @0 AND a.{n.Date} <= @1", filter.FromUtc, filter.ToUtc);
        sql.Append($"AND a.{n.EventType} LIKE @0 ESCAPE '{CoreSql.LikeEscape}'", PrefixPattern(filter.Prefix));

        if (filter.UserKey is { } key)
        {
            int id = filter.UserId ?? int.MinValue;
            if (filter.DetailsUserPattern is { } pattern)
            {
                sql.Append(
                    $"AND (a.{n.PerformerKey} = @0 OR (a.{n.PerformerKey} IS NULL AND a.{n.PerformerId} = @1) OR a.{n.EventDetails} LIKE @2 ESCAPE '{CoreSql.LikeEscape}')",
                    key, id, CoreSql.ContainsPattern(pattern));
            }
            else
            {
                sql.Append($"AND (a.{n.PerformerKey} = @0 OR (a.{n.PerformerKey} IS NULL AND a.{n.PerformerId} = @1))", key, id);
            }
        }

        if (filter.Search is { } search)
        {
            sql.Append(
                $"AND (a.{n.EventType} LIKE @0 ESCAPE '{CoreSql.LikeEscape}' OR a.{n.EventDetails} LIKE @0 ESCAPE '{CoreSql.LikeEscape}')",
                CoreSql.ContainsPattern(search));
        }

        if (!count)
        {
            var direction = filter.Ascending ? "ASC" : "DESC";
            sql.Append($"ORDER BY a.{n.Date} {direction}, a.{n.Id} {direction}");
        }

        return sql;
    }

    public static Sql<ISqlContext> ClaimedById(ISqlContext ctx, int id, string prefix)
    {
        Names n = new(ctx.SqlSyntax);
        return ctx.Sql(Columns(n)).Append($"FROM {n.Table} a")
            .Append($"WHERE a.{n.Id} = @0", id)
            .Append($"AND a.{n.EventType} LIKE @0 ESCAPE '{CoreSql.LikeEscape}'", PrefixPattern(prefix));
    }

    public static Sql<ISqlContext> ById(ISqlContext ctx, int id, IReadOnlyCollection<string> excludedPrefixes)
    {
        Names n = new(ctx.SqlSyntax);
        Sql<ISqlContext> sql = ctx.Sql(Columns(n)).Append($"FROM {n.Table} a").Append($"WHERE a.{n.Id} = @0", id);
        foreach (var prefix in excludedPrefixes)
        {
            sql.Append($"AND a.{n.EventType} NOT LIKE @0 ESCAPE '{CoreSql.LikeEscape}'", PrefixPattern(prefix));
        }

        return sql;
    }

    public static Sql<ISqlContext> MinMaxId(ISqlContext ctx, bool max)
    {
        Names n = new(ctx.SqlSyntax);
        return ctx.Sql($"SELECT {(max ? "MAX" : "MIN")}(a.{n.Id}) FROM {n.Table} a");
    }

    /// <summary>The first row at or after (or the last at or before) an id: a primary-key seek.</summary>
    public static Sql<ISqlContext> Probe(ISqlContext ctx, int id, bool atOrAfter)
    {
        Names n = new(ctx.SqlSyntax);
        return ctx.Sql($"SELECT a.{n.Id} AS Id, a.{n.Date} AS EventDateUtc FROM {n.Table} a")
            .Append(atOrAfter ? $"WHERE a.{n.Id} >= @0 ORDER BY a.{n.Id} ASC" : $"WHERE a.{n.Id} <= @0 ORDER BY a.{n.Id} DESC", id);
    }

    private static string PrefixPattern(string prefix) => CoreSql.ContainsPattern(prefix)[1..];

    private static string Columns(Names n)
        => $"SELECT a.{n.Id} AS Id, a.{n.PerformerId} AS PerformingUserId, a.{n.PerformerKey} AS PerformingUserKey, " +
           $"a.{n.PerformerDetails} AS PerformingDetails, a.{n.PerformerIp} AS PerformingIp, a.{n.Date} AS EventDateUtc, " +
           $"a.{n.AffectedId} AS AffectedUserId, a.{n.AffectedKey} AS AffectedUserKey, a.{n.AffectedDetails} AS AffectedDetails, " +
           $"a.{n.EventType} AS EventType, a.{n.EventDetails} AS EventDetails";

    private sealed class Names(ISqlSyntaxProvider s)
    {
        public string Table { get; } = s.GetQuotedTableName("umbracoAudit");
        public string Id { get; } = s.GetQuotedColumnName("id");
        public string PerformerId { get; } = s.GetQuotedColumnName("performingUserId");
        public string PerformerKey { get; } = s.GetQuotedColumnName("performingUserKey");
        public string PerformerDetails { get; } = s.GetQuotedColumnName("performingDetails");
        public string PerformerIp { get; } = s.GetQuotedColumnName("performingIp");
        public string Date { get; } = s.GetQuotedColumnName("eventDateUtc");
        public string AffectedId { get; } = s.GetQuotedColumnName("affectedUserId");
        public string AffectedKey { get; } = s.GetQuotedColumnName("affectedUserKey");
        public string AffectedDetails { get; } = s.GetQuotedColumnName("affectedDetails");
        public string EventType { get; } = s.GetQuotedColumnName("eventType");
        public string EventDetails { get; } = s.GetQuotedColumnName("eventDetails");
    }
}

internal sealed class AuditRow
{
    public int Id { get; set; }

    public int PerformingUserId { get; set; }

    public Guid? PerformingUserKey { get; set; }

    public string? PerformingDetails { get; set; }

    public string? PerformingIp { get; set; }

    public DateTime EventDateUtc { get; set; }

    public int AffectedUserId { get; set; }

    public Guid? AffectedUserKey { get; set; }

    public string? AffectedDetails { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string? EventDetails { get; set; }
}

internal sealed class AuditProbeRow
{
    public int Id { get; set; }

    public DateTime EventDateUtc { get; set; }
}
