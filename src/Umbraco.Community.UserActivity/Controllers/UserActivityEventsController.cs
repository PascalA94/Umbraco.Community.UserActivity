using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Umbraco.Community.UserActivity.Controllers.Models;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Security;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Extensions;

namespace Umbraco.Community.UserActivity.Controllers;

/// <summary>
/// The per-user Activity tab (design 6.2): readers for any user including themselves; with <c>AllowSelfView</c>,
/// a non-reader for their own key only. Anything else is 403, including a real key that isn't the caller's.
/// </summary>
[ApiVersion("1.0")]
public sealed class UserActivityEventsController : UserActivityControllerBase
{
    private readonly ActivityRequestParser _parser;
    private readonly ActivityQueryEngine _engine;
    private readonly IAuthorizationService _authorization;
    private readonly ViewLogger _viewLogger;

    public UserActivityEventsController(ActivityRequestParser parser, ActivityQueryEngine engine, IAuthorizationService authorization, ViewLogger viewLogger)
    {
        _parser = parser;
        _engine = engine;
        _authorization = authorization;
        _viewLogger = viewLogger;
    }

    [HttpGet("users/{userKey:guid}/events")]
    [MapToApiVersion("1.0")]
    [Authorize(Policy = UserActivityPolicy.SelfOrRead)]
    [EndpointName("GetUserActivityEvents")]
    [ProducesResponseType(typeof(ActivityPageResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets a page of one user's activity, as the actor or as Via.")]
    public async Task<IActionResult> GetUserActivityEvents(
        Guid userKey,
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
        var subject = new ActivityActor(ActorKind.User, userKey, null, null, null);
        if (!(await _authorization.AuthorizeAsync(User, subject, UserActivitySubjectRequirement.Instance)).Succeeded)
        {
            return Forbidden();
        }

        var request = new ActivityRequest { Source = source, Category = category, EntityType = entityType, From = from, To = to, Search = search, Skip = skip, Take = take, Order = order };
        ParseResult parsed = _parser.Parse(request, userKey);
        if (parsed.Query is null)
        {
            return InvalidParameter(parsed.InvalidParameter!);
        }

        ActivityPage page = await _engine.QueryAsync(parsed.Query, cancellationToken);
        if (page.UserNotFound)
        {
            return NotFound();
        }

        await _viewLogger.LogViewAsync(User.Identity!.GetUserKey()!.Value, userKey, cancellationToken);
        return Ok(ActivityResponseMapper.ToModel(page, await IsReaderAsync(_authorization)));
    }
}
