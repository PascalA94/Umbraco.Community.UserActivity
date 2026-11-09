using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Export;
using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Security;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Extensions;

namespace Umbraco.Community.UserActivity.Controllers;

/// <summary>
/// CSV export of the global view's filters (design 6.6): readers only, and only when <c>Export:Enabled</c> (404
/// otherwise). At most <c>Export:MaxRows</c> rows, one export at a time per user, every export logged.
/// </summary>
[ApiVersion("1.0")]
public sealed class ActivityExportController : UserActivityControllerBase
{
    private readonly ActivityRequestParser _parser;
    private readonly ActivityQueryEngine _engine;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly ExportGate _gate;
    private readonly ViewLogger _viewLogger;
    private readonly TimeProvider _time;

    public ActivityExportController(
        ActivityRequestParser parser,
        ActivityQueryEngine engine,
        IOptionsMonitor<UserActivityOptions> options,
        ExportGate gate,
        ViewLogger viewLogger,
        TimeProvider time)
    {
        _parser = parser;
        _engine = engine;
        _options = options;
        _gate = gate;
        _viewLogger = viewLogger;
        _time = time;
    }

    [HttpGet("events/export")]
    [MapToApiVersion("1.0")]
    [Authorize(Policy = UserActivityPolicy.Read)]
    [EndpointName("ExportActivityEvents")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [EndpointSummary("Exports the filtered activity as CSV, when export is enabled.")]
    public async Task<IActionResult> ExportActivityEvents(
        [FromQuery] string? userKey,
        [FromQuery] string[]? source,
        [FromQuery] string[]? category,
        [FromQuery] string[]? entityType,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? search,
        [FromQuery] string? order,
        CancellationToken cancellationToken)
    {
        UserActivityOptions options = _options.CurrentValue;
        if (!options.Export.Enabled)
        {
            return NotFound();
        }

        Guid? subject = null;
        if (userKey is not null)
        {
            if (!Guid.TryParse(userKey, out Guid parsedKey))
            {
                return InvalidParameter("userKey");
            }

            subject = parsedKey;
        }

        var request = new ActivityRequest { Source = source, Category = category, EntityType = entityType, From = from, To = to, Search = search, Order = order };
        ParseResult parsed = _parser.Parse(request, subject);
        if (parsed.Query is null)
        {
            return InvalidParameter(parsed.InvalidParameter!);
        }

        Guid viewer = User.Identity!.GetUserKey()!.Value;
        if (!_gate.TryEnter(viewer))
        {
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        try
        {
            // One row over the cap tells us the filters match too much (design 6.6).
            ActivityPage page = await _engine.QueryAsync(parsed.Query with { Skip = 0, Take = options.Export.MaxRows + 1 }, cancellationToken);
            if (page.UserNotFound)
            {
                return InvalidParameter("userKey");
            }

            // An export is relied on as complete evidence: never hand over a file with a source missing (M1 review QA-3).
            var missing = page.Sources.Where(s => s.Status is SourceStatus.Failed or SourceStatus.TimedOut).Select(s => s.Alias).ToList();
            if (missing.Count > 0)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new ProblemDetailsBuilder()
                        .WithTitle("Incomplete export")
                        .WithDetail($"These sources did not answer, so no file was made: {string.Join(", ", missing)}. Try again or narrow the filters.")
                        .Build());
            }

            if (page.Items.Count > options.Export.MaxRows || (page.TotalIsExact && page.Total > options.Export.MaxRows))
            {
                return BadRequest(new ProblemDetailsBuilder().WithTitle("Too many rows").WithDetail("Too many rows; narrow the filters.").Build());
            }

            await _viewLogger.LogExportAsync(viewer, subject, page.Items.Count, cancellationToken);
            Response.Headers.XContentTypeOptions = "nosniff";
            return File(CsvWriter.Write(Rows(page, options.ShowIpAddresses)), "text/csv; charset=utf-8", CsvWriter.FileName(_time.GetUtcNow()));
        }
        finally
        {
            _gate.Exit(viewer);
        }
    }

    private static IEnumerable<IReadOnlyList<string?>> Rows(ActivityPage page, bool showIp)
    {
        string[] header = ["OccurredUtc", "User", "UserKey", "Via", "Category", "RawType", "Source", "ItemType", "ItemName", "ItemKey", "Summary"];
        yield return showIp ? [.. header[..10], "IpAddress", header[10]] : header;

        foreach (ActivityEvent e in page.Items)
        {
            ActorDisplay actor = ActorDisplay.Compose(e.Actor, page.Actors);
            ActorDisplay? via = e.Actor.Via is null ? null : ActorDisplay.Compose(e.Actor.Via, page.Actors);
            string?[] row =
            [
                e.OccurredUtc.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                actor.Name ?? actor.Label,
                actor.UserKey?.ToString("D"),
                via is null ? null : via.Name ?? via.Label,
                e.Category,
                e.RawType,
                e.Source,
                e.Entity?.Type,
                e.Entity?.Name,
                e.Entity?.Key,
                e.Summary,
            ];
            yield return showIp ? [.. row[..10], e.IpAddress, row[10]] : row;
        }
    }
}
