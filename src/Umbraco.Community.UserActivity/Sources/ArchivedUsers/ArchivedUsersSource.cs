using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Sources.ArchivedUsers;

/// <summary>
/// The <c>archivedUsers</c> source: Archived Users (01) writes one core <c>umbracoLog</c> row per action (S5), so
/// this reads core tables and references nothing of 01 (Pascal, 2026-11-07). It claims only 01's rows (entity type
/// <c>User</c>, header <c>Custom</c>, comment <c>ArchivedUsers:</c>): <c>User</c> alone would take core's user rows.
/// Available when the <c>Umbraco.Community.ArchivedUsers</c> assembly is loaded, checked by name.
/// </summary>
internal sealed partial class ArchivedUsersSource : IUserActivitySource, IClaimsCoreAuditRows
{
    public const string AssemblyName = "Umbraco.Community.ArchivedUsers";

    /// <summary>01's <c>ArchiveAuditAction</c> names. Archiving and restoring have no category of their own (plan Task 24).</summary>
    internal static IReadOnlyDictionary<string, string> CategoryByAction { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Archived"] = Categories.Other,
        ["Unarchived"] = Categories.Other,
        ["AutoArchived"] = Categories.Other,
        ["AutoDisabled"] = Categories.Other,
        ["Exported"] = Categories.Exported,
        ["AuditPurged"] = Categories.Deleted,
    };

    private static readonly ClaimedLogRows Claim = new("User", "Custom", "ArchivedUsers:");

    private readonly IScopeProvider _scopes;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly Func<string, bool> _isLoaded;
    private bool _installed;

    public ArchivedUsersSource(IScopeProvider scopes, IOptionsMonitor<UserActivityOptions> options)
        : this(scopes, options, OptionalAssemblyGuard.IsLoaded)
    {
    }

    internal ArchivedUsersSource(IScopeProvider scopes, IOptionsMonitor<UserActivityOptions> options, Func<string, bool> isLoaded)
    {
        _scopes = scopes;
        _options = options;
        _isLoaded = isLoaded;
    }

    public string Alias => SourceAlias.ArchivedUsers;

    public string DisplayNameKey => "userActivity_sourceArchivedUsers";

    public bool IsAvailable => _installed || (_installed = _isLoaded(AssemblyName));

    public ActivitySourceInfo Info { get; } = new(["user"], [Categories.Other, Categories.Exported, Categories.Deleted], CanCount: true, CanSearch: true, RetentionHint: null);

    public IReadOnlyCollection<string> ClaimedAuditEventTypePrefixes { get; } = [];

    public IReadOnlyCollection<string> ClaimedLogEntityTypes { get; } = [];

    public IReadOnlyCollection<ClaimedLogRows> ClaimedLogRows { get; } = [Claim];

    public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        if (query.EntityTypes.Count > 0 && !query.EntityTypes.Contains("user"))
        {
            return Task.FromResult(new ActivitySourcePage([], 0, false));
        }

        List<string>? actions = null;
        var includeOther = false;
        if (query.Categories.Count > 0)
        {
            actions = CategoryByAction.Where(p => p.Value != Categories.Other && query.Categories.Contains(p.Value)).Select(p => p.Key).ToList();
            includeOther = query.Categories.Contains(Categories.Other);
            if (includeOther)
            {
                actions.AddRange(CategoryByAction.Where(p => p.Value == Categories.Other).Select(p => p.Key));
            }

            if (actions.Count == 0 && !includeOther)
            {
                return Task.FromResult(new ActivitySourcePage([], 0, false));
            }
        }

        var filter = new ArchivedUsersSql.Filter(query.FromUtc.UtcDateTime, query.ToUtc.UtcDateTime, query.User?.Id, actions, includeOther, query.Search, query.Ascending);
        using IScope scope = _scopes.CreateScope(autoComplete: true);
        CoreTimeouts.Apply(scope, _options.CurrentValue);
        ct.ThrowIfCancellationRequested();
        List<LogRow> rows = scope.Database.SkipTake<LogRow>(0, query.Limit + 1, ArchivedUsersSql.Rows(scope.SqlContext, Claim, filter, count: false));
        ct.ThrowIfCancellationRequested();
        int? total = CoreTimeouts.ShouldCount(query) ? scope.Database.ExecuteScalar<int>(ArchivedUsersSql.Rows(scope.SqlContext, Claim, filter, count: true)) : null;

        var items = rows.Take(query.Limit).Select(r => Map(r, withDetails: false)).ToList();
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
        LogRow? row = scope.Database.FirstOrDefault<LogRow>(ArchivedUsersSql.ById(scope.SqlContext, Claim, logId));
        return Task.FromResult(row is null ? null : Map(row, withDetails: true));
    }

    internal static ActivityEvent Map(LogRow row, bool withDetails)
    {
        (string? action, string? source) = Parse(row.LogComment);
        ActivityActor actor = row.UserId is { } userId
            ? new ActivityActor(ActorKind.User, null, userId, null, null)
            : new ActivityActor(ActorKind.System, null, null, null, null);
        var target = row.NodeId.ToString(CultureInfo.InvariantCulture);

        return new ActivityEvent(
            SourceAlias.ArchivedUsers,
            CoreLogSource.IdPrefix + row.Id.ToString(CultureInfo.InvariantCulture),
            new DateTimeOffset(DateTime.SpecifyKind(row.Datestamp, DateTimeKind.Utc)),
            actor,
            action is not null ? CategoryByAction[action] : Categories.Other,
            action ?? "ArchivedUsers",
            new ActivityEntity("user", null, target, null, true),
            Text.Cap(row.LogComment, ActivityEvent.MaxSummaryLength),
            null,
            withDetails ? Details(row, action, source, target) : null);
    }

    /// <summary>"ArchivedUsers: Archived (Manual)" to its known action and source; anything else is not trusted as either.</summary>
    private static (string? Action, string? Source) Parse(string? comment)
    {
        Match match = comment is null ? Match.Empty : CommentPattern().Match(comment);
        if (!match.Success || !CategoryByAction.ContainsKey(match.Groups["action"].Value))
        {
            return (null, null);
        }

        return (match.Groups["action"].Value, match.Groups["source"].Value);
    }

    private static Dictionary<string, string> Details(LogRow row, string? action, string? source, string target)
    {
        var details = new Dictionary<string, string> { ["targetUserId"] = target, ["logHeader"] = row.LogHeader };
        Text.AddCapped(details, "action", action);
        Text.AddCapped(details, "source", source);
        Text.AddCapped(details, "comment", row.LogComment);
        return details;
    }

    [GeneratedRegex(@"^ArchivedUsers: (?<action>[A-Za-z]{1,32}) \((?<source>[A-Za-z]{1,32})\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex CommentPattern();
}
