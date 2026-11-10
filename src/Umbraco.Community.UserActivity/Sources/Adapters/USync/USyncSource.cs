using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using uSync.History;
using uSync.History.Service;

namespace Umbraco.Community.UserActivity.Sources.Adapters.USync;

/// <summary>
/// The <c>usync</c> source over uSync's <see cref="ISyncHistoryService"/> (uSync 17.3.0+, design 3.2): one event per
/// import or export run. uSync stores only the username, so a user's tab matches it by username, ignoring case, and
/// a renamed user's older runs show under the stored name (design 3.5). History has no paging, so only the newest
/// <see cref="MaxEntries"/> entries are read. The history file's server path is never exposed.
/// </summary>
internal sealed class USyncSource : IUserActivitySource, IRuntimeAdapter
{
    public const string IdPrefix = "usync:";
    public const string BackgroundLabel = "uSync (no user)";
    public const int MaxEntries = 5_000;

    private readonly IServiceScopeFactory _scopes;
    private readonly IServiceProviderIsService _isService;

    public USyncSource(IServiceScopeFactory scopes, IServiceProviderIsService isService)
    {
        _scopes = scopes;
        _isService = isService;
    }

    public string Alias => SourceAlias.USync;

    public string DisplayNameKey => "userActivity_sourceUSync";

    /// <summary>Hidden when uSync's history is switched off: there is nothing to read.</summary>
    public bool IsAvailable => _isService.IsService(typeof(ISyncHistoryService)) && IsEnabled();

    /// <summary>uSync keeps history until someone clears it, so there is no fixed retention to report.</summary>
    public ActivitySourceInfo Info { get; } = new([], [Categories.Imported, Categories.Exported, Categories.Other], CanCount: true, CanSearch: true, RetentionHint: null);

    public string? IntegrationVersion => typeof(ISyncHistoryService).Assembly.GetName().Version?.ToString();

    // Every method that names a uSync type is synchronous and awaits nothing: see AdapterTasks for why.
    public async Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        if (query.EntityTypes.Count > 0 || (query.Categories.Count > 0 && !query.Categories.Overlaps(Info.Categories)))
        {
            return new ActivitySourcePage([], 0, false);
        }

        using IServiceScope scope = _scopes.CreateScope();
        History history = await ReadAsync(scope.ServiceProvider);
        ct.ThrowIfCancellationRequested();

        var matches = history.Events
            .Where(e => e.OccurredUtc >= query.FromUtc && e.OccurredUtc <= query.ToUtc)
            .Where(e => query.Categories.Count == 0 || query.Categories.Contains(e.Category))
            // Username only (never the email): it's the one identity uSync stored.
            .Where(e => query.User is null || (e.Actor.StoredName is { } name && query.User.Username is { } username
                && e.Actor.Kind == ActorKind.User && string.Equals(name, username, StringComparison.OrdinalIgnoreCase)))
            .Where(e => query.Search is null || Contains(e.RawType, query.Search) || Contains(e.Actor.StoredName, query.Search))
            .Order(query.Ascending ? ActivityEventOrder.Ascending : ActivityEventOrder.Descending)
            .ToList();

        return new ActivitySourcePage(
            matches.Take(query.Limit).ToList(),
            history.Capped ? null : matches.Count,
            history.Capped || matches.Count > query.Limit);
    }

    public async Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
    {
        if (!id.StartsWith(IdPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        using IServiceScope scope = _scopes.CreateScope();
        History history = await ReadAsync(scope.ServiceProvider, withDetails: true);
        return history.Events.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Asked on every request, outside the engine's per-source failure handling, so it must never throw: a uSync
    /// that can't answer (a changed signature, a service that fails to build) hides the source (M3 review QA-1).
    /// </summary>
    private bool IsEnabled()
    {
        try
        {
            return CallIsEnabled();
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Separate so a missing member fails when this is JIT-compiled, inside IsEnabled's try.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private bool CallIsEnabled()
    {
        using IServiceScope scope = _scopes.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ISyncHistoryService>().IsEnabled();
    }

    private static Task<History> ReadAsync(IServiceProvider services, bool withDetails = false)
        => AdapterTasks.Map(
            services.GetRequiredService<ISyncHistoryService>().GetHistoryAsync(),
            entries => ToHistory(entries, withDetails));

    private static History ToHistory(IEnumerable<HistoryInfo>? entries, bool withDetails)
    {
        var all = (entries ?? []).Where(e => e is not null).ToList();
        var newest = all.OrderByDescending(e => Utc(e.Date)).Take(MaxEntries).Select(e => Map(e, withDetails)).ToList();
        return new History(newest, all.Count > MaxEntries);
    }

    internal static ActivityEvent Map(HistoryInfo entry, bool withDetails)
    {
        DateTime utc = Utc(entry.Date);
        var method = Text.Cap(entry.Method, 100) ?? "uSync";
        ActivityActor actor = string.IsNullOrEmpty(entry.Username)
            ? new ActivityActor(ActorKind.System, null, null, BackgroundLabel, null)
            : new ActivityActor(ActorKind.User, null, null, Text.Cap(entry.Username, 200), null);

        return new ActivityEvent(
            SourceAlias.USync,
            Id(utc, entry.FilePath),
            new DateTimeOffset(utc),
            actor,
            Category(method),
            method,
            null,
            $"{method}: {entry.Changes.ToString(CultureInfo.InvariantCulture)} changes in {entry.Total.ToString(CultureInfo.InvariantCulture)} items",
            null,
            withDetails ? Details(entry, method) : null);
    }

    private static string Category(string method)
        => method.StartsWith("import", StringComparison.OrdinalIgnoreCase) ? Categories.Imported
            : method.StartsWith("export", StringComparison.OrdinalIgnoreCase) ? Categories.Exported
            : Categories.Other;

    /// <summary>
    /// uSync stores the time as written; a local time is converted, and an unspecified one is taken as UTC (as
    /// Umbraco's own tables are).
    /// </summary>
    private static DateTime Utc(DateTime value)
        => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>Opaque and stable: the time plus a hash of the history file, so the server path never leaves.</summary>
    private static string Id(DateTime utc, string? filePath)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(filePath ?? string.Empty)))[..12];
        return $"{IdPrefix}{utc.Ticks.ToString(CultureInfo.InvariantCulture)}-{hash}";
    }

    private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

    private static Dictionary<string, string> Details(HistoryInfo entry, string method)
    {
        var details = new Dictionary<string, string>
        {
            ["method"] = method,
            ["changes"] = entry.Changes.ToString(CultureInfo.InvariantCulture),
            ["total"] = entry.Total.ToString(CultureInfo.InvariantCulture),
        };
        Text.AddCapped(details, "username", entry.Username);
        return details;
    }

    private sealed record History(IReadOnlyList<ActivityEvent> Events, bool Capped);
}
