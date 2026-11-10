using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Umbraco.Community.UserActivity.Controllers.Models;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Security;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Extensions;

namespace Umbraco.Community.UserActivity.Controllers;

/// <summary>
/// One event with its details (design 6.3). The event is loaded first and the access rule is applied to its actor
/// and <c>Via</c> (design 7.2), so a self-view user can't open someone else's event by guessing an id. A
/// non-reader gets 403 for a missing id too, so ids can't be probed.
/// </summary>
[ApiVersion("1.0")]
public sealed class ActivityEventDetailController : UserActivityControllerBase
{
    private readonly ActivityQueryEngine _engine;
    private readonly IAuthorizationService _authorization;
    private readonly ViewLogger _viewLogger;

    public ActivityEventDetailController(ActivityQueryEngine engine, IAuthorizationService authorization, ViewLogger viewLogger)
    {
        _engine = engine;
        _authorization = authorization;
        _viewLogger = viewLogger;
    }

    [HttpGet("events/{source}/{id}")]
    [MapToApiVersion("1.0")]
    [Authorize(Policy = UserActivityPolicy.SelfOrRead)]
    [EndpointName("GetActivityEvent")]
    [ProducesResponseType(typeof(ActivityEventDetailResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets one activity event with its details.")]
    public async Task<IActionResult> GetActivityEvent(string source, string id, CancellationToken cancellationToken)
    {
        var isReader = await IsReaderAsync(_authorization);
        ActivityDetail? detail = await _engine.GetAsync(source, id, cancellationToken);

        if (!isReader)
        {
            if (detail is null || !(await _authorization.AuthorizeAsync(User, detail.Event.Actor, UserActivitySubjectRequirement.Instance)).Succeeded)
            {
                return Forbidden();
            }
        }

        if (detail is null)
        {
            return NotFound();
        }

        // Opening someone's event is viewing their activity (M1 review SEC-2): log the actor and Via, throttled.
        Guid viewer = User.Identity!.GetUserKey()!.Value;
        foreach (Guid subject in Subjects(detail))
        {
            await _viewLogger.LogViewAsync(viewer, subject, cancellationToken);
        }

        return Ok(ActivityResponseMapper.ToDetail(detail, isReader));
    }

    private static IEnumerable<Guid> Subjects(ActivityDetail detail)
    {
        ActivityActor actor = detail.Event.Actor;
        return new[] { actor, actor.Via }
            .OfType<ActivityActor>()
            .Select(a => ActorDisplay.Compose(a, detail.Actors).UserKey)
            .OfType<Guid>()
            .Distinct();
    }
}
