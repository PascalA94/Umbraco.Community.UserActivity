using System.Net;

namespace Umbraco.Community.UserActivity.IntegrationTests.Api;

/// <summary>
/// M1 review SEC-8: our GETs write audit rows (views, exports), so a cross-site link must not be able to call them.
/// The Management API needs the bearer header, which a top-level navigation can't add, and the token cookie is
/// SameSite=Strict. Checked on both lines; if core ever accepts the cookie alone, this fails.
/// </summary>
[TestFixture]
public class CrossSiteRequestTests : ApiTestBase
{
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
