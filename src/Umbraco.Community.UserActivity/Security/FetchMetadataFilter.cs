#if UMBRACO_19
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Common.Builders;
using Umbraco.Cms.Core.Configuration.Models;

namespace Umbraco.Community.UserActivity.Security;

/// <summary>
/// The decision, kept pure so it unit-tests without a host. A present <c>Sec-Fetch-Site</c> value other than
/// <c>same-origin</c> or <c>none</c> is refused (fail closed). The policy refuses an empty value, but the ASP.NET Core test host delivers one as absent, so only the unit test covers it; the only exception is
/// a request whose <c>Origin</c> is the configured back-office host. An absent header is allowed: non-browser clients,
/// bearer API users and old Safari send none, and <c>SameSite=Strict</c> stays the second layer.
/// </summary>
public static class FetchMetadataPolicy
{
    public static bool IsAllowed(IEnumerable<string> secFetchSite, string? origin, Uri? backOfficeHost)
    {
        string[] values = secFetchSite.ToArray();
        if (values.Length == 0)
        {
            return true;
        }

        if (values.All(v => v is "same-origin" or "none"))
        {
            return true;
        }

        // Compares scheme, host and port (default ports normalised), so "https://host.evil.test" cannot pass as "host".
        return backOfficeHost is not null
            && Uri.TryCreate(origin, UriKind.Absolute, out Uri? o)
            && Uri.Compare(o, backOfficeHost, UriComponents.SchemeAndServer, UriFormat.UriEscaped, StringComparison.OrdinalIgnoreCase) == 0;
    }
}

/// <summary>
/// Umbraco 19 accepts the back-office auth cookie alone and has no anti-forgery, so a same-site attacker, or a site
/// configured with <c>AuthCookieSameSite</c> None or Lax, could forge the GETs that write audit rows (views, exports).
/// <c>SameSite=Strict</c> alone does not cover that. 17 and 18 rely on the required bearer header instead (security
/// finding F1, regresses M1 review SEC-8). A resource filter runs after authentication (unauthenticated callers still
/// get 401) and before model binding and the action, so a refused request writes no audit row and takes no export lock.
/// </summary>
public sealed class FetchMetadataFilter : IResourceFilter
{
    private readonly IOptionsMonitor<SecuritySettings> _security;
    private readonly ILogger<FetchMetadataFilter> _logger;

    public FetchMetadataFilter(IOptionsMonitor<SecuritySettings> security, ILogger<FetchMetadataFilter> logger)
    {
        _security = security;
        _logger = logger;
    }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        HttpRequest request = context.HttpContext.Request;
        string[] site = request.Headers["Sec-Fetch-Site"].ToArray()!;
        if (FetchMetadataPolicy.IsAllowed(site, request.Headers.Origin.ToString(), _security.CurrentValue.BackOfficeHost))
        {
            return;
        }

        // Never log the Origin (attacker-controlled) or an arbitrary header value: only a fixed token.
        _logger.LogWarning("Refused a cross-site request to the User Activity API ({FetchSite})", site.Contains("same-site") ? "same-site" : site.Contains("cross-site") ? "cross-site" : "other");
        context.Result = new ObjectResult(new ProblemDetailsBuilder().WithTitle("Forbidden").Build())
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
#endif
