using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.IntegrationTests.Core;

namespace Umbraco.Community.UserActivity.IntegrationTests.Api;

/// <summary>Task 12: the Management API over real authentication and policies (design 5, 7.2, Review Focus 1).</summary>
[TestFixture]
public class UserActivityApiTests : ApiTestBase
{
    private const string Base = ManagementApi + "user-activity/";

    private UserActivityOptions Options => Services.GetRequiredService<IOptionsMonitor<UserActivityOptions>>().CurrentValue;

    private IScopeProvider Scopes => Services.GetRequiredService<IScopeProvider>();

    [SetUp]
    public void ResetOptions()
    {
        Options.Export.Enabled = false;
        Options.AllowSelfView = false;
        Options.AllowedUserGroups = null;
        Options.ShowIpAddresses = false;
    }

    private static string Window => $"from={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(5).ToString("O"))}";

    private void LogFor(IUser user, string comment) => CoreSeed.Log(Scopes, user.Id, 1, "Document", "Save", comment, DateTime.UtcNow.AddMinutes(-1));

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string text = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), text);
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    [Test]
    public async Task Admin_200_OnEveryEndpoint_Editor403_Anonymous401()
    {
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        LogFor(wren, "wren row");
        HttpClient adminClient = await CreateAuthenticatedClientAsync(admin);
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);
        HttpClient anonymous = CreateAnonymousClient();

        JsonElement global = await Json(await adminClient.GetAsync(Base + "events?" + Window));
        string id = global.GetProperty("items").EnumerateArray().First(i => i.GetProperty("summary").GetString() == "wren row").GetProperty("id").GetString()!;
        string[] urls =
        [
            Base + "events?" + Window,
            Base + $"users/{wren.Key}/events?" + Window,
            Base + $"events/core/{Uri.EscapeDataString(id)}",
            Base + "filter-options",
            Base + "events/export",
        ];
        Options.Export.Enabled = true;

        foreach (var url in urls)
        {
            Assert.That((await adminClient.GetAsync(url)).StatusCode, Is.EqualTo(HttpStatusCode.OK), url);
            Assert.That((await edClient.GetAsync(url)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), url);
            Assert.That((await anonymous.GetAsync(url)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), url);
        }
    }

    [Test]
    public async Task Admin_OwnTab_200()
    {
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        LogFor(admin, "own row");
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        JsonElement page = await Json(await client.GetAsync(Base + $"users/{admin.Key}/events?" + Window));

        Assert.That(page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("summary").GetString()), Does.Contain("own row"));
    }

    [Test]
    public async Task PerUser_ReturnsOnlyThatUser_WithResolvedActor()
    {
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        LogFor(wren, "wren row");
        LogFor(ed, "ed row");
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        JsonElement page = await Json(await client.GetAsync(Base + $"users/{wren.Key}/events?" + Window));

        JsonElement item = page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("summary").GetString() == "wren row");
        Assert.That(page.GetProperty("items").EnumerateArray().Any(i => i.GetProperty("summary").GetString() == "ed row"), Is.False);
        Assert.That(item.GetProperty("actor").GetProperty("kind").GetString(), Is.EqualTo("user"));
        Assert.That(item.GetProperty("actor").GetProperty("name").GetString(), Is.EqualTo("Wren Writer"));
        Assert.That(item.GetProperty("actor").GetProperty("userKey").GetGuid(), Is.EqualTo(wren.Key));
        Assert.That(item.GetProperty("source").GetString(), Is.EqualTo("core"));
        Assert.That(item.GetProperty("category").GetString(), Is.EqualTo("saved"));
        Assert.That(item.TryGetProperty("details", out _), Is.False);
        Assert.That(page.GetProperty("totalIsExact").GetBoolean(), Is.True);
        Assert.That(page.GetProperty("depthCapped").GetBoolean(), Is.False);
        Assert.That(page.GetProperty("sources")[0].GetProperty("alias").GetString(), Is.EqualTo("core"));
        Assert.That(page.GetProperty("sources")[0].GetProperty("status").GetString(), Is.EqualTo("ok"));
    }

    [Test]
    public async Task PerUser_UnknownUser_404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Assert.That((await client.GetAsync(Base + $"users/{Guid.NewGuid()}/events")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PerUser_OtherKey_403()
    {
        Options.AllowSelfView = true;
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);

        Assert.That((await edClient.GetAsync(Base + $"users/{wren.Key}/events")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await edClient.GetAsync(Base + $"users/{Guid.NewGuid()}/events")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task PerUser_Self_NonAdmin_200_OnlyWhenAllowSelfView()
    {
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        LogFor(ed, "ed row");
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);
        string url = Base + $"users/{ed.Key}/events?" + Window;

        Assert.That((await edClient.GetAsync(url)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Options.AllowSelfView = true;
        JsonElement page = await Json(await edClient.GetAsync(url));

        Assert.That(page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("summary").GetString()), Does.Contain("ed row"));
    }

    [Test]
    public async Task Detail_OtherActor_403()
    {
        Options.AllowSelfView = true;
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        LogFor(wren, "wren row");
        HttpClient admin = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());
        string id = (await Json(await admin.GetAsync(Base + $"users/{wren.Key}/events?" + Window))).GetProperty("items")[0].GetProperty("id").GetString()!;
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);

        Assert.That((await edClient.GetAsync(Base + $"events/core/{Uri.EscapeDataString(id)}")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        // A missing id looks the same to a self-view user, so ids can't be probed.
        Assert.That((await edClient.GetAsync(Base + "events/core/log%3A999999")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Detail_SelfViewUser_OwnEvent_200_WithoutDetails()
    {
        Options.AllowSelfView = true;
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        // An Automate-shaped row: Wren started it, Ed's account performed it. Until M3 claims the prefix it is a core
        // row by Ed; the Via path is covered by the engine tests.
        CoreSeed.Audit(Scopes, ed.Id, ed.Key, "umbraco/user/save", DateTime.UtcNow.AddMinutes(-1), affectedUserId: wren.Id, affectedUserKey: wren.Key);
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);
        JsonElement page = await Json(await edClient.GetAsync(Base + $"users/{ed.Key}/events?" + Window));
        string id = page.GetProperty("items").EnumerateArray().First(i => i.GetProperty("rawType").GetString() == "umbraco/user/save").GetProperty("id").GetString()!;

        JsonElement detail = await Json(await edClient.GetAsync(Base + $"events/core/{Uri.EscapeDataString(id)}"));

        // SEC-1: details are for readers only.
        Assert.That(detail.GetProperty("rawType").GetString(), Is.EqualTo("umbraco/user/save"));
        Assert.That(detail.TryGetProperty("details", out JsonElement details) && details.ValueKind != JsonValueKind.Null, Is.False);
    }

    [Test]
    public async Task Detail_SelfViewUser_OwnSignIn_NeverShowsTheAdminsDetails()
    {
        // SEC-1: on a user's own sign-in row the performer is the default admin (S5), whose name and email core
        // stores in performingDetails.
        Options.AllowSelfView = true;
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        CoreSeed.Audit(Scopes, -1, Constants.Security.SuperUserKey, "umbraco/user/sign-in/login", DateTime.UtcNow.AddMinutes(-1),
            affectedUserId: ed.Id, affectedUserKey: ed.Key, performingDetails: "User \"SentinelAdmin\" <sentinel.admin@example.test>", eventDetails: "SentinelEvent", affectedDetails: "SentinelAffected");
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);
        // Signing Ed in writes a real login row too; open the seeded one.
        string id;
        using (Umbraco.Cms.Infrastructure.Scoping.IScope scope = Scopes.CreateScope(autoComplete: true))
        {
            id = "audit:" + scope.Database.ExecuteScalar<int>("SELECT id FROM umbracoAudit WHERE eventDetails = @0", "SentinelEvent");
        }

        string body = await (await edClient.GetAsync(Base + $"events/core/{Uri.EscapeDataString(id)}")).Content.ReadAsStringAsync();
        HttpClient admin = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());
        string adminBody = await admin.GetStringAsync(Base + $"events/core/{Uri.EscapeDataString(id)}");

        Assert.That(body, Does.Contain("signedIn"));
        Assert.That(body, Does.Not.Contain("Sentinel").And.Not.Contain("sentinel"));
        Assert.That(adminBody, Does.Contain("SentinelAdmin"), "readers still see the details");
    }

    [Test]
    public async Task Detail_UnknownForAdmin_404()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Assert.That((await client.GetAsync(Base + "events/core/log%3A999999")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await client.GetAsync(Base + "events/nope/log%3A1")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Global_SelfViewOnly_403()
    {
        Options.AllowSelfView = true;
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);

        Assert.That((await edClient.GetAsync(Base + $"events?userKey={ed.Key}")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That((await edClient.GetAsync(Base + "events")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task FilterOptions_SelfViewUser_200_NonAdminNoSelfView_403()
    {
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);

        Assert.That((await edClient.GetAsync(Base + "filter-options")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Options.AllowSelfView = true;
        JsonElement options = await Json(await edClient.GetAsync(Base + "filter-options"));

        Assert.That(options.GetProperty("canReadAll").GetBoolean(), Is.False);
        Assert.That(options.GetProperty("exportEnabled").GetBoolean(), Is.False);
    }

    [Test]
    public async Task FilterOptions_ListsOnlyActiveSources()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        JsonElement options = await Json(await client.GetAsync(Base + "filter-options"));
        Options.Sources["Core"] = new SourceOptions { Enabled = false };
        JsonElement disabled = await Json(await client.GetAsync(Base + "filter-options"));
        Options.Sources.Clear();

        Assert.That(options.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("alias").GetString()), Is.EqualTo(new[] { "core" }));
        Assert.That(options.GetProperty("categories").EnumerateArray().Select(c => c.GetString()), Does.Contain("signedIn"));
        Assert.That(options.GetProperty("entityTypes").EnumerateArray().Select(c => c.GetString()), Does.Contain("document"));
        Assert.That(options.GetProperty("canReadAll").GetBoolean(), Is.True);
        Assert.That(options.GetProperty("defaultRangeDays").GetInt32(), Is.EqualTo(30));
        Assert.That(disabled.GetProperty("sources").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task CustomGroup_InAllowedUserGroups_200_WithoutUsersSection_403()
    {
        IUserGroupService groups = Services.GetRequiredService<IUserGroupService>();
        await groups.CreateAsync(Group("auditors", ["users"]), Constants.Security.SuperUserKey);
        await groups.CreateAsync(Group("watchers", ["content"]), Constants.Security.SuperUserKey);
        Options.AllowedUserGroups = ["ADMIN", "Auditors", "watchers"];
        HttpClient auditor = await CreateAuthenticatedClientAsync(await Users.CreateAsync("Ada Auditor", ["auditors"]));
        HttpClient watcher = await CreateAuthenticatedClientAsync(await Users.CreateAsync("Will Watcher", ["watchers"]));

        Assert.That((await auditor.GetAsync(Base + "events")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await watcher.GetAsync(Base + "events")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "AllowedGroup_WithoutUsersSection_403");
    }

    [TestCase("source=nope", "source")]
    [TestCase("source=", "source")]
    [TestCase("category=nope", "category")]
    [TestCase("entityType=nope", "entityType")]
    [TestCase("take=101", "take")]
    [TestCase("take=0", "take")]
    [TestCase("skip=-1", "skip")]
    [TestCase("search=ab", "search")]
    [TestCase("order=sideways", "order")]
    [TestCase("from=2026-01-01T00:00:00Z&to=2025-01-01T00:00:00Z", "from")]
    [TestCase("from=2024-01-01T00:00:00Z&to=2026-01-01T00:00:00Z", "from")]
    [TestCase("to=0001-01-05T00:00:00Z", "to")]
    [TestCase("from=1700-01-01T00:00:00Z", "from")]
    [TestCase("to=9999-12-31T00:00:00Z", "to")]
    [TestCase("from=not-a-date", "from")]
    public async Task InvalidParameter_400_NamesParameterOnly(string query, string parameter)
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        HttpResponseMessage response = await client.GetAsync(Base + "events?" + query);

        string text = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), text);
        string detail = JsonDocument.Parse(text).RootElement.GetProperty("detail").GetString()!;
        Assert.That(detail, Does.Contain($"'{parameter}'"));
        Assert.That(text, Does.Not.Contain("nope").And.Not.Contain("sideways"));
    }

    [Test]
    public async Task SearchWithControlChars_400_And101Chars_400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Assert.That((await client.GetAsync(Base + "events?search=abc%00def")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await client.GetAsync(Base + "events?search=" + new string('a', 101))).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await client.GetAsync(Base + "events?search=" + new string('a', 100))).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DisabledSource_400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());
        Options.Sources["core"] = new SourceOptions { Enabled = false };
        try
        {
            HttpResponseMessage response = await client.GetAsync(Base + "events?source=core");
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }
        finally
        {
            Options.Sources.Clear();
        }
    }

    [Test]
    public async Task NoStore_OnEveryResponse()
    {
        IUser admin = await Users.CreateAdminAsync();
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        foreach (var url in new[] { Base + "events", Base + "events?take=500", Base + "filter-options", Base + $"users/{admin.Key}/events", Base + "events/core/log%3A999999" })
        {
            HttpResponseMessage response = await client.GetAsync(url);
            Assert.That(response.Headers.CacheControl?.NoStore, Is.True, $"{url} {(int)response.StatusCode}");
        }
    }

    [Test]
    public async Task IpAddress_OnlyForReadersWhenOn()
    {
        Options.AllowSelfView = true;
        IUser admin = await Users.CreateAdminAsync();
        IUser ed = await Users.CreateEditorAsync("Ed Editor");
        CoreSeed.Audit(Scopes, ed.Id, ed.Key, "umbraco/user/save", DateTime.UtcNow.AddMinutes(-1), performingIp: "203.0.113.99");
        HttpClient adminClient = await CreateAuthenticatedClientAsync(admin);
        HttpClient edClient = await CreateAuthenticatedClientAsync(ed);
        string url = Base + $"users/{ed.Key}/events?" + Window;

        Assert.That(await adminClient.GetStringAsync(url), Does.Not.Contain("203.0.113.99"));
        Options.ShowIpAddresses = true;
        Assert.That(await adminClient.GetStringAsync(url), Does.Contain("203.0.113.99"));
        Assert.That(await edClient.GetStringAsync(url), Does.Not.Contain("203.0.113.99"), "IPs are admin-only (P8)");
    }

    private IUserGroup Group(string alias, string[] sections)
    {
        var group = new UserGroup(Services.GetRequiredService<Umbraco.Cms.Core.Strings.IShortStringHelper>(), 0, alias, alias, null);
        foreach (var section in sections)
        {
            group.AddAllowedSection(section);
        }

        return group;
    }
}
