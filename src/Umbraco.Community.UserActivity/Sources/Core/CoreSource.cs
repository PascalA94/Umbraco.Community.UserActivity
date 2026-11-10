using System.Globalization;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;

namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// The built-in <c>core</c> source: core's <c>umbracoLog</c> and <c>umbracoAudit</c> tables, read-only, minus the
/// rows other sources claim (design 3.6). Always available.
/// </summary>
public sealed class CoreSource : IUserActivitySource
{
    private readonly CoreLogSource _log;
    private readonly CoreAuditSource _audit;

    public CoreSource(IScopeProvider scopes, CoreClaims claims, IOptionsMonitor<UserActivityOptions> options)
    {
        _log = new CoreLogSource(scopes, claims, options);
        _audit = new CoreAuditSource(scopes, claims, options);
    }

    public string Alias => SourceAlias.Core;

    public string DisplayNameKey => "userActivity_sourceCore";

    public bool IsAvailable => true;

    public ActivitySourceInfo Info { get; } = new(
        ["document", "media", "member", "documentType", "mediaType", "memberType", "dataType", "template", "user", "userGroup"],
        Categories.All,
        CanCount: true,
        CanSearch: true,
        RetentionHint: null);

    /// <summary>Top N from each table, merged in <see cref="ActivityEventOrder"/>; the total is the sum of both counts.</summary>
    public async Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        ActivitySourcePage log = await _log.QueryAsync(query, ct);
        ActivitySourcePage audit = await _audit.QueryAsync(query, ct);

        var merged = log.Items.Concat(audit.Items)
            .Order(query.Ascending ? ActivityEventOrder.Ascending : ActivityEventOrder.Descending)
            .ToList();
        return new ActivitySourcePage(
            merged.Take(query.Limit).ToList(),
            log.Total is { } logTotal && audit.Total is { } auditTotal ? logTotal + auditTotal : null,
            log.Truncated || audit.Truncated || merged.Count > query.Limit);
    }

    public Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
    {
        if (id.StartsWith(CoreLogSource.IdPrefix, StringComparison.Ordinal)
            && int.TryParse(id.AsSpan(CoreLogSource.IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var logId))
        {
            return _log.GetAsync(logId, ct);
        }

        if (id.StartsWith(CoreAuditSource.IdPrefix, StringComparison.Ordinal)
            && int.TryParse(id.AsSpan(CoreAuditSource.IdPrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var auditId))
        {
            return _audit.GetAsync(auditId, ct);
        }

        return Task.FromResult<ActivityEvent?>(null);
    }
}
