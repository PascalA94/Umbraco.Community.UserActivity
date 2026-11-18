using System.Net;
#if UMBRACO_19
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Security;
#endif

namespace Umbraco.Community.UserActivity.IntegrationTests.Api;

/// <summary>
/// M1 review SEC-8: our GETs write audit rows (views, exports), so a cross-site link must not be able to call them.
/// The Management API needs the bearer header, which a top-level navigation can't add, and the token cookie is
/// SameSite=Strict. Checked on both lines; if core ever accepts the cookie alone, this fails.
/// </summary>
[TestFixture]
public class CrossSiteRequestTests : ApiTestBase
{
#if !UMBRACO_19
    [Test]
    public async Task CookieWithoutAuthorizationHeader_401_AndTokenCookieIsSameSiteStrict()
    {
        (HttpClient client, HttpResponseMessage token) = await AuthenticateAsync(await Users.CreateAdminAsync());
        Assert.That((await client.GetAsync(ManagementApi + "user-activity/filter-options")).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        client.DefaultRequestHeaders.Authorization = null;

        Assert.That((await client.GetAsync(ManagementApi + "user-activity/filter-options")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        string cookie = token.Headers.GetValues("Set-Cookie").Last(c => c.StartsWith("__Host-umbAccessToken=", StringComparison.Ordinal) && !c.Contains("1970"));
        Assert.That(cookie.ToLowerInvariant(), Does.Contain("samesite=strict"));
    }
}
#else
    // On 19 the cookie alone authenticates, so the bearer-header control is gone. The control is the Sec-Fetch-Site
    // filter (FetchMetadataFilter); every refusal must also leave no audit row behind.
    private const string Base = ManagementApi + "user-activity/";

    private IScopeProvider Scopes => Services.GetRequiredService<IScopeProvider>();

    [SetUp]
    public void ResetOptions()
    {
        UserActivityOptions options = Services.GetRequiredService<IOptionsMonitor<UserActivityOptions>>().CurrentValue;
        options.LogViews = true;
        options.Export.Enabled = true;
        options.Export.MaxRows = 10000;
    }

    private int Rows(string eventType)
    {
        using IScope scope = Scopes.CreateScope(autoComplete: true);
        return scope.Database.ExecuteScalar<int>("SELECT COUNT(*) FROM umbracoAudit WHERE eventType = @0", eventType);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? fetchSite, string? origin = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (fetchSite is not null)
        {
            request.Headers.Add("Sec-Fetch-Site", fetchSite);
        }

        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }

        return client.SendAsync(request);
    }

    [Test]
    public async Task LoginCookie_IsStrictHttpOnlySecure()
    {
        (_, HttpResponseMessage login) = await AuthenticateAsync(await Users.CreateAdminAsync());

        string cookie = login.Headers.GetValues("Set-Cookie").Last(c => c.StartsWith("UMB_UCONTEXT=", StringComparison.Ordinal) && !c.Contains("1970")).ToLowerInvariant();

        Assert.That(cookie, Does.Contain("samesite=strict"));
        Assert.That(cookie, Does.Contain("httponly"));
        Assert.That(cookie, Does.Contain("secure"));
    }

    [TestCase("cross-site")]
    [TestCase("same-site")]
    // No empty-value case: the test host reports 200 for an empty Sec-Fetch-Site, i.e. it treats it as absent. The policy
    // itself refuses "" (FetchMetadataPolicyTests).
    public async Task CrossSiteAndSameSite_403_OnUserEvents_AndNoViewRow(string fetchSite)
    {
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        HttpResponseMessage response = await GetAsync(client, Base + $"users/{wren.Key}/events", fetchSite);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(Rows(ViewLogger.ViewedEventType), Is.EqualTo(0));
    }

    [Test]
    public async Task CrossSite_403_OnGlobalEvents_AndNoGlobalViewRow()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        HttpResponseMessage response = await GetAsync(client, Base + "events", "cross-site");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(Rows(ViewLogger.ViewedEventType), Is.EqualTo(0));
    }

    [Test]
    public async Task CrossSite_403_OnExport_AndNoExportRow_AndLockNotTaken()
    {
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        HttpResponseMessage refused = await GetAsync(client, Base + $"events/export?userKey={wren.Key}", "cross-site");

        Assert.That(refused.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(Rows(ViewLogger.ExportedEventType), Is.EqualTo(0));

        HttpResponseMessage allowed = await GetAsync(client, Base + $"events/export?userKey={wren.Key}", "same-origin");

        Assert.That(allowed.StatusCode, Is.EqualTo(HttpStatusCode.OK), "not 429: the refused call must not take the export lock");
        Assert.That(Rows(ViewLogger.ExportedEventType), Is.EqualTo(1));
    }

    [TestCase("same-origin")]
    [TestCase("none")]
    [TestCase(null)]
    public async Task SameOriginNoneAndNoHeader_200_AndOneViewRow(string? fetchSite)
    {
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        HttpResponseMessage response = await GetAsync(client, Base + $"users/{wren.Key}/events", fetchSite);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(Rows(ViewLogger.ViewedEventType), Is.EqualTo(1));
    }
}

/// <summary>The one Origin exception: a configured back-office host (dev-server setups).</summary>
[TestFixture]
public class BackOfficeHostCrossSiteTests : ApiTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.Configure<SecuritySettings>(s => s.BackOfficeHost = new Uri("https://backoffice.example.test"));
    }

    private static Task<HttpResponseMessage> CrossSiteAsync(HttpClient client, string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ManagementApi + "user-activity/filter-options");
        request.Headers.Add("Sec-Fetch-Site", "cross-site");
        request.Headers.Add("Origin", origin);
        return client.SendAsync(request);
    }

    [Test]
    public async Task CrossSite_WithOriginEqualToBackOfficeHost_200()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Assert.That((await CrossSiteAsync(client, "https://backoffice.example.test")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CrossSite_WithOtherOrigin_403()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Assert.That((await CrossSiteAsync(client, "https://evil.test")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }
}
#endif
