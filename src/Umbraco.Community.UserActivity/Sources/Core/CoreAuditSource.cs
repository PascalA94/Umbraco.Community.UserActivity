using System.Globalization;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;

namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// Reads core's <c>umbracoAudit</c> (users, groups, members, sign-ins, passwords, keyed by user key). One half of
/// the <c>core</c> source. Rows claimed by another source (Automate's <c>umbraco/automate/</c>) are skipped.
/// </summary>
internal sealed class CoreAuditSource
{
    public const string IdPrefix = "audit:";

    private readonly IScopeProvider _scopes;
    private readonly CoreClaims _claims;
    private readonly IOptionsMonitor<UserActivityOptions> _options;

    public CoreAuditSource(IScopeProvider scopes, CoreClaims claims, IOptionsMonitor<UserActivityOptions> options)
    {
        _scopes = scopes;
        _claims = claims;
        _options = options;
    }

    public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        (IReadOnlyCollection<string>? eventTypes, bool includeOther, bool none) = EventTypeFilter(query.Categories);
        List<string> entityPrefixes = query.EntityTypes
            .SelectMany(e => AuditEventTypeMap.EntityTypePrefixes.TryGetValue(e, out IReadOnlyList<string>? p) ? p : [])
            .ToList();
        if (none || (query.EntityTypes.Count > 0 && entityPrefixes.Count == 0))
        {
            return Task.FromResult(new ActivitySourcePage([], 0, false));
        }

        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        ct.ThrowIfCancellationRequested();
        (int Low, int High)? range = IdRange(scope, query.FromUtc.UtcDateTime, query.ToUtc.UtcDateTime);
        if (range is null)
        {
            return Task.FromResult(new ActivitySourcePage([], 0, false));
        }

        var filter = new CoreAuditSql.AuditFilter(
            range.Value.Low,
            range.Value.High,
            query.FromUtc.UtcDateTime,
            query.ToUtc.UtcDateTime,
            query.User?.Key,
            query.User?.Id,
            eventTypes,
            includeOther,
            entityPrefixes,
            _claims.AuditPrefixes,
            query.Search,
            query.Ascending);

        ct.ThrowIfCancellationRequested();
        List<AuditRow> rows = scope.Database.SkipTake<AuditRow>(0, query.Limit + 1, CoreAuditSql.Audit(scope.SqlContext, filter, count: false));
        ct.ThrowIfCancellationRequested();
        int? total = CoreTimeouts.ShouldCount(query) ? scope.Database.ExecuteScalar<int>(CoreAuditSql.Audit(scope.SqlContext, filter, count: true)) : null;
        var showIp = _options.CurrentValue.ShowIpAddresses;
        var items = rows.Take(query.Limit).Select(r => Map(r, showIp, withDetails: false)).ToList();
        return Task.FromResult(new ActivitySourcePage(items, total, rows.Count > query.Limit));
    }

    public Task<ActivityEvent?> GetAsync(int id, CancellationToken ct)
    {
        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        AuditRow? row = scope.Database.FirstOrDefault<AuditRow>(CoreAuditSql.ById(scope.SqlContext, id, _claims.AuditPrefixes));
        return Task.FromResult(row is null ? null : Map(row, _options.CurrentValue.ShowIpAddresses, withDetails: true));
    }

    /// <summary>
    /// The first and last id inside the window, by binary search over primary-key seeks (about 2 x log2(rows)
    /// single-row reads), shared by every source over <c>umbracoAudit</c>. Ids grow with time, so the range covers the window; a row written with a back-dated
    /// timestamp can fall just outside it. Null when no row is inside.
    /// </summary>
    internal static (int Low, int High)? IdRange(IScope scope, DateTime fromUtc, DateTime toUtc)
    {
        int? min = scope.Database.ExecuteScalar<int?>(CoreAuditSql.MinMaxId(scope.SqlContext, max: false));
        int? max = scope.Database.ExecuteScalar<int?>(CoreAuditSql.MinMaxId(scope.SqlContext, max: true));
        if (min is null || max is null)
        {
            return null;
        }

        int? low = null;
        int lo = min.Value, hi = max.Value;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) / 2);
            AuditProbeRow? row = Probe(scope, mid, atOrAfter: true);
            if (row is null || row.Id > hi)
            {
                hi = mid - 1;
            }
            else if (Utc(row.EventDateUtc) >= fromUtc)
            {
                low = row.Id;
                hi = mid - 1;
            }
            else
            {
                lo = row.Id + 1;
            }
        }

        int? high = null;
        lo = min.Value;
        hi = max.Value;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) / 2);
            AuditProbeRow? row = Probe(scope, mid, atOrAfter: false);
            if (row is null || row.Id < lo)
            {
                lo = mid + 1;
            }
            else if (Utc(row.EventDateUtc) <= toUtc)
            {
                high = row.Id;
                lo = mid + 1;
            }
            else
            {
                hi = row.Id - 1;
            }
        }

        return low is { } l && high is { } h && l <= h ? (l, h) : null;
    }

    private static AuditProbeRow? Probe(IScope scope, int id, bool atOrAfter)
        => scope.Database.SkipTake<AuditProbeRow>(0, 1, CoreAuditSql.Probe(scope.SqlContext, id, atOrAfter)).FirstOrDefault();

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static (IReadOnlyCollection<string>? Types, bool IncludeOther, bool None) EventTypeFilter(IReadOnlySet<string> categories)
    {
        if (categories.Count == 0)
        {
            return (null, false, false);
        }

        var types = categories.SelectMany(AuditEventTypeMap.EventTypesFor).Distinct(StringComparer.Ordinal).ToList();
        var includeOther = categories.Contains(Categories.Other);
        return (types, includeOther, types.Count == 0 && !includeOther);
    }

    private static ActivityEvent Map(AuditRow row, bool showIp, bool withDetails)
        => new(
            SourceAlias.Core,
            IdPrefix + row.Id.ToString(CultureInfo.InvariantCulture),
            new DateTimeOffset(Utc(row.EventDateUtc)),
            Actor(row),
            AuditEventTypeMap.ToCategory(row.EventType),
            row.EventType,
            Entity(row),
            // No summary: eventDetails and performingDetails can hold what someone typed as a username (design 7.2).
            null,
            showIp ? row.PerformingIp : null,
            withDetails ? Details(row) : null);

    private static ActivityActor Actor(AuditRow row)
    {
        switch (row.EventType)
        {
            case AuditEventTypeMap.SignInLogin or AuditEventTypeMap.SignInLogout:
                return new ActivityActor(ActorKind.User, row.AffectedUserKey, row.AffectedUserId == 0 ? null : row.AffectedUserId, null, null);
            case AuditEventTypeMap.SignInFailed:
                return new ActivityActor(ActorKind.Unknown, null, null, null, null);
        }

        return row.PerformingUserId == 0 && row.PerformingUserKey is null
            ? new ActivityActor(ActorKind.System, null, 0, null, null)
            : new ActivityActor(ActorKind.User, row.PerformingUserKey, row.PerformingUserId, null, null);
    }

    private static ActivityEntity? Entity(AuditRow row)
    {
        var type = AuditEventTypeMap.ToEntityType(row.EventType);
        if (type is null)
        {
            return null;
        }

        var hasAffected = row.AffectedUserKey is not null || row.AffectedUserId != 0;
        return type == "userGroup" || !hasAffected
            ? new ActivityEntity(type, null, null, null, true)
            : new ActivityEntity(type, row.AffectedUserKey?.ToString("D"), row.AffectedUserId.ToString(CultureInfo.InvariantCulture), null, true);
    }

    /// <summary>The IP is never in the details: only <see cref="ActivityEvent.IpAddress"/> carries it, so one setting controls it.</summary>
    private static Dictionary<string, string> Details(AuditRow row)
    {
        var details = new Dictionary<string, string>
        {
            ["eventType"] = row.EventType,
            ["performingUserId"] = row.PerformingUserId.ToString(CultureInfo.InvariantCulture),
            ["affectedUserId"] = row.AffectedUserId.ToString(CultureInfo.InvariantCulture),
        };
        Text.AddCapped(details, "performingUserKey", row.PerformingUserKey?.ToString("D"));
        Text.AddCapped(details, "affectedUserKey", row.AffectedUserKey?.ToString("D"));
        Text.AddCapped(details, "performingDetails", row.PerformingDetails);
        Text.AddCapped(details, "affectedDetails", row.AffectedDetails);
        Text.AddCapped(details, "eventDetails", row.EventDetails);
        return details;
    }
}
