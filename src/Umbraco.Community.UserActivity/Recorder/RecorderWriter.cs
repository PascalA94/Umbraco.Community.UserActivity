using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>
/// Stores one recorded event (design 4.1). It is called from another package's notification handler, so it must
/// never make that package's save fail: every failure is swallowed and logged as one Warning with the source and the
/// exception type only (design 7.4), never the exception message, which can carry the values being written.
/// </summary>
internal sealed class RecorderWriter : IRecorderWriter
{
    private readonly IScopeProvider _scopes;
    private readonly TimeProvider _time;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly ILogger<RecorderWriter> _logger;

    public RecorderWriter(IScopeProvider scopes, TimeProvider time, IOptionsMonitor<UserActivityOptions> options, ILogger<RecorderWriter> logger)
    {
        _scopes = scopes;
        _time = time;
        _options = options;
        _logger = logger;
    }

    public void Write(RecorderEntry entry)
    {
        try
        {
            // Switching the source off stops recording too: an owner who turned it off did not ask us to keep storing
            // what people do behind a view nobody can open.
            if (_options.CurrentValue.Sources.TryGetValue(SourceAlias.Recorder, out SourceOptions? source) && source is { Enabled: false })
            {
                return;
            }

            ActivityEventDto dto = ToDto(entry, _time.GetUtcNow().UtcDateTime);

            // A fresh scope per write. When the caller has no scope of its own (CSP Manager, UrlTracker and Workflow, S4)
            // this is our own transaction; when it has one (Skybrud's delete, S4) ours joins it. Umbraco rolls a joined
            // transaction back if a child scope ends without Complete, so a failed insert here would silently undo the
            // other package's save. A single INSERT either lands or leaves nothing behind, so completing in finally
            // is safe: our failure is logged below and stays ours.
            using IScope scope = _scopes.CreateScope();
            try
            {
                scope.Database.Insert(dto);
            }
            finally
            {
                scope.Complete();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Recorder could not store an event for source {SourceAlias} ({ExceptionType}).", Safe(entry?.Source), ex.GetType().Name);
        }
    }

    internal static ActivityEventDto ToDto(RecorderEntry entry, DateTime occurredUtc)
        => new()
        {
            OccurredUtc = occurredUtc,
            Source = Text.Cap(entry.Source, RecorderTable.SourceMax) ?? string.Empty,
            Category = Categories.IsKnown(entry.Category) ? entry.Category : Categories.Other,
            RawType = Text.Cap(entry.RawType, RecorderTable.RawTypeMax) ?? string.Empty,
            UserKey = entry.UserKey,
            UserName = Text.Cap(entry.UserName, RecorderTable.UserNameMax),
            EntityType = Text.Cap(entry.EntityType, RecorderTable.EntityTypeMax) ?? string.Empty,
            EntityKey = Text.Cap(entry.EntityKey, RecorderTable.EntityKeyMax),
            EntityName = Text.Cap(entry.EntityName, RecorderTable.EntityNameMax),
            DetailsJson = SerialiseDetails(entry.Details),
        };

    /// <summary>
    /// A JSON object of string values. Keys are added in order and one that would push the text over the column is
    /// left out whole, so the stored value is always valid JSON (a cut-off string would not parse).
    /// </summary>
    private static string? SerialiseDetails(IReadOnlyDictionary<string, string>? details)
    {
        if (details is null || details.Count == 0)
        {
            return null;
        }

        var kept = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in details)
        {
            if (kept.Count >= ActivityEvent.MaxDetails)
            {
                break;
            }

            var cappedKey = Text.Cap(key, RecorderTable.RawTypeMax)!;
            if (kept.ContainsKey(cappedKey))
            {
                continue;
            }

            kept[cappedKey] = Text.Cap(value ?? string.Empty, ActivityEvent.MaxDetailLength)!;
            if (JsonSerializer.Serialize(kept).Length > RecorderTable.DetailsMax)
            {
                kept.Remove(cappedKey);
            }
        }

        return kept.Count == 0 ? null : JsonSerializer.Serialize(kept);
    }

    /// <summary>The source comes from a handler's own constant, but it still reaches a log line, so it is shortened.</summary>
    private static string Safe(string? source) => Text.Cap(source, RecorderTable.SourceMax) ?? string.Empty;
}
