using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Umbraco.Community.UserActivity.Controllers.Models;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Security;
using Umbraco.Extensions;

namespace Umbraco.Community.UserActivity.Controllers;

/// <summary>The global view under Users (design 6.1): readers only, every filter including a user.</summary>
[ApiVersion("1.0")]
public sealed class ActivityEventsController : UserActivityControllerBase
{
    private readonly ActivityRequestParser _parser;
    private readonly ActivityQueryEngine _engine;
    private readonly ViewLogger _viewLogger;

    public ActivityEventsController(ActivityRequestParser parser, ActivityQueryEngine engine, ViewLogger viewLogger)
    {
        _parser = parser;
        _engine = engine;
        _viewLogger = viewLogger;
    }

    [HttpGet("events")]
    [MapToApiVersion("1.0")]
    [Authorize(Policy = UserActivityPolicy.Read)]
    [EndpointName("GetActivityEvents")]
    [ProducesResponseType(typeof(ActivityPageResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [EndpointSummary("Gets a page of activity across users and sources.")]
    public async Task<IActionResult> GetActivityEvents(
        [FromQuery] string? userKey,
        [FromQuery] string[]? source,
        [FromQuery] string[]? category,
        [FromQuery] string[]? entityType,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? search,
        [FromQuery] string? skip,
        [FromQuery] string? take,
        [FromQuery] string? order,
        CancellationToken cancellationToken)
    {
        Guid? key = null;
        if (userKey is not null)
        {
            if (!Guid.TryParse(userKey, out Guid parsed))
            {
                return InvalidParameter("userKey");
            }

            key = parsed;
        }

        var request = new ActivityRequest { Source = source, Category = category, EntityType = entityType, From = from, To = to, Search = search, Skip = skip, Take = take, Order = order };
        ParseResult parsedRequest = _parser.Parse(request, key);
        if (parsedRequest.Query is null)
        {
            return InvalidParameter(parsedRequest.InvalidParameter!);
        }

        ActivityPage page = await _engine.QueryAsync(parsedRequest.Query, cancellationToken);
        if (page.UserNotFound)
        {
            return InvalidParameter("userKey");
        }

        // Filtering the global view to one person is viewing their activity, so it is logged like the tab; the
        // unfiltered view shows everyone and is logged once per hour with no subject (design 7.1, M1 review SEC-2).
        Guid viewer = User.Identity!.GetUserKey()!.Value;
        if (key is { } subject)
        {
            await _viewLogger.LogViewAsync(viewer, subject, cancellationToken);
        }
        else
        {
            await _viewLogger.LogGlobalViewAsync(viewer, cancellationToken);
        }

        return Ok(ActivityResponseMapper.ToModel(page, isReader: true));
    }
}
