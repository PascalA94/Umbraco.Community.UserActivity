using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using Umbraco.Community.UserActivity.Security;

namespace Umbraco.Community.UserActivity.Controllers;

/// <summary>
/// S1 [Critical]: every route is a Management API route (backoffice token required), GET only, never anonymous,
/// and each action carries a <see cref="UserActivityPolicy"/>. Responses are never cached (design 5).
/// </summary>
[VersionedApiBackOfficeRoute(RouteSegment)]
[ApiExplorerSettings(GroupName = UserActivityApi.GroupName)]
[MapToApi(UserActivityApi.DocumentName)]
[NoStore]
public abstract class UserActivityControllerBase : ManagementApiControllerBase
{
    public const string RouteSegment = "user-activity";

    /// <summary>A 400 that names the parameter only, never its value (design 5).</summary>
    protected IActionResult InvalidParameter(string parameter)
        => BadRequest(new ProblemDetailsBuilder()
            .WithTitle("Invalid parameter")
            .WithDetail($"The '{parameter}' parameter is not valid.")
            .Build());

    protected async Task<bool> IsReaderAsync(IAuthorizationService authorization)
        => (await authorization.AuthorizeAsync(User, UserActivityPolicy.Read)).Succeeded;
}

public static class UserActivityApi
{
    public const string DocumentName = "user-activity";

    public const string GroupName = "User Activity";
}

/// <summary>
/// <c>Cache-Control: no-store</c> on every response these controllers produce, set before model binding so 400s
/// carry it too. (401 and 403 from the authorization middleware never reach MVC filters.)
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class NoStoreAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        context.HttpContext.Response.Headers.Pragma = "no-cache";
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
