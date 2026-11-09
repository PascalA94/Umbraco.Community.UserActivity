using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Export;
using Umbraco.Community.UserActivity.IntegrationTests.Core;

namespace Umbraco.Community.UserActivity.IntegrationTests.Api;

/// <summary>Task 13: CSV export (design 6.6, 7.3) and the audit rows views and exports write (design 7.1).</summary>
[TestFixture]
public class ExportApiTests : ApiTestBase
{
    private const string Base = ManagementApi + "user-activity/";

    private UserActivityOptions Options => Services.GetRequiredService<IOptionsMonitor<UserActivityOptions>>().CurrentValue;

    private IScopeProvider Scopes => Services.GetRequiredService<IScopeProvider>();

    [SetUp]
    public void ResetOptions()
    {
        Options.Export.Enabled = true;
        Options.Export.MaxRows = 10000;
        Options.AllowSelfView = false;
        Options.ShowIpAddresses = false;
        Options.LogViews = true;
    }

    private void Log(IUser user, string comment, int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            CoreSeed.Log(Scopes, user.Id, 1, "Document", "Save", comment, DateTime.UtcNow.AddMinutes(-1 - i));
        }
    }

    [Test]
    public async Task Export_ReturnsCsv_WithHeaders_AndFormulaPrefix()
    {
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        Log(wren, "=HYPERLINK(\"http://x\")");
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        HttpResponseMessage response = await client.GetAsync(Base + $"events/export?userKey={wren.Key}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(response.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/csv"));
        Assert.That(response.Content.Headers.ContentDisposition!.DispositionType, Is.EqualTo("attachment"));
        Assert.That(response.Content.Headers.ContentDisposition.FileName?.Trim('"'), Does.Match(@"^user-activity-\d{8}-\d{4}\.csv$"));
        Assert.That(response.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
        Assert.That(response.Headers.CacheControl?.NoStore, Is.True);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.That(bytes[..3], Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        string csv = Encoding.UTF8.GetString(bytes[3..]);
        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines[0], Is.EqualTo("\"OccurredUtc\",\"User\",\"UserKey\",\"Via\",\"Category\",\"RawType\",\"Source\",\"ItemType\",\"ItemName\",\"ItemKey\",\"Summary\""));
        Assert.That(csv, Does.Contain("\"'=HYPERLINK(\"\"http://x\"\")\""));
        Assert.That(csv, Does.Contain("\"Wren Writer\""));
    }

    [Test]
    public async Task Export_Disabled_404()
    {
        Options.Export.Enabled = false;
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Assert.That((await client.GetAsync(Base + "events/export")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Export_OverMaxRows_400()
    {
        Options.Export.MaxRows = 3;
        IUser admin = await Users.CreateAdminAsync();
        Log(admin, "row", count: 4);
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        HttpResponseMessage response = await client.GetAsync(Base + $"events/export?userKey={admin.Key}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("narrow the filters"));
    }

    [Test]
    public async Task Export_SecondConcurrent_429()
    {
        IUser admin = await Users.CreateAdminAsync();
        HttpClient client = await CreateAuthenticatedClientAsync(admin);
        ExportGate gate = Services.GetRequiredService<ExportGate>();
        Assert.That(gate.TryEnter(admin.Key), Is.True, "simulates an export already running for this user");
        try
        {
            Assert.That((await client.GetAsync(Base + "events/export")).StatusCode, Is.EqualTo(HttpStatusCode.TooManyRequests));
        }
        finally
        {
            gate.Exit(admin.Key);
        }

        Assert.That((await client.GetAsync(Base + "events/export")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Export_NoIpColumnByDefault()
    {
        IUser admin = await Users.CreateAdminAsync();
        CoreSeed.Audit(Scopes, admin.Id, admin.Key, "umbraco/user/save", DateTime.UtcNow.AddMinutes(-1), performingIp: "203.0.113.42");
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        string off = await client.GetStringAsync(Base + "events/export");
        Options.ShowIpAddresses = true;
        string on = await client.GetStringAsync(Base + "events/export");

        Assert.That(off, Does.Not.Contain("IpAddress").And.Not.Contain("203.0.113.42"));
        Assert.That(on, Does.Contain("\"IpAddress\"").And.Contain("203.0.113.42"));
    }

    [Test]
    public async Task Export_SelfViewUser_403()
    {
        Options.AllowSelfView = true;
        IUser ed = await Users.CreateEditorAsync();
        HttpClient client = await CreateAuthenticatedClientAsync(ed);

        Assert.That((await client.GetAsync(Base + $"events/export?userKey={ed.Key}")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Export_InvalidParameter_400()
    {
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        Assert.That((await client.GetAsync(Base + "events/export?source=nope")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task DetailOpen_And_UnfilteredGlobalView_AreLoggedAsViews()
    {
        // SEC-2: browsing everyone, or opening one person's event, is recorded too.
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        Log(wren, "wren row");
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        string global = await client.GetStringAsync(Base + "events");
        string id = System.Text.Json.JsonDocument.Parse(global).RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("summary").GetString() == "wren row").GetProperty("id").GetString()!;

        await client.GetAsync(Base + "events");
        await client.GetAsync(Base + $"events/core/{Uri.EscapeDataString(id)}");
        await client.GetAsync(Base + $"events/core/{Uri.EscapeDataString(id)}");

        using IScope scope = Scopes.CreateScope(autoComplete: true);
        List<Guid?> affected = scope.Database.Fetch<Guid?>(
            "SELECT affectedUserKey FROM umbracoAudit WHERE eventType = @0 AND performingUserKey = @1", "community/useractivity/viewed", admin.Key);
        Assert.That(affected.Count(a => a is null), Is.EqualTo(1), "one row per hour for the unfiltered view");
        Assert.That(affected.Count(a => a == wren.Key), Is.EqualTo(1), "one row per hour for the opened event's actor");
    }

    [Test]
    public async Task ViewRows_StoreNoViewerIp()
    {
        IUser admin = await Users.CreateAdminAsync();
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        await client.GetAsync(Base + $"users/{wren.Key}/events");

        using IScope scope = Scopes.CreateScope(autoComplete: true);
        var ips = scope.Database.Fetch<string?>("SELECT performingIp FROM umbracoAudit WHERE eventType = @0", "community/useractivity/viewed");
        Assert.That(ips, Is.Not.Empty);
        Assert.That(ips.All(string.IsNullOrEmpty), Is.True);
    }

    [Test]
    public async Task ViewsAndExports_WriteAuditRows_ThatShowInCore()
    {
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        await client.GetAsync(Base + $"users/{wren.Key}/events");
        await client.GetAsync(Base + $"users/{wren.Key}/events?take=10");
        await client.GetAsync(Base + $"events/export?userKey={wren.Key}");
        string page = await client.GetStringAsync(Base + $"users/{admin.Key}/events?category=other&category=exported");

        Assert.That(CountOf(page, "community/useractivity/viewed"), Is.EqualTo(1), "throttled to one row per viewer and subject per hour");
        Assert.That(CountOf(page, "community/useractivity/exported"), Is.EqualTo(1));
    }

    [Test]
    public async Task LogViewsOff_NoAuditRows()
    {
        Options.LogViews = false;
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        IUser wren = await Users.CreateAsync("Wren Writer", ["writer"]);
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        await client.GetAsync(Base + $"users/{wren.Key}/events");
        await client.GetAsync(Base + "events/export");
        string page = await client.GetStringAsync(Base + $"users/{admin.Key}/events");

        Assert.That(page, Does.Not.Contain("community/useractivity/"));
    }

    private static int CountOf(string text, string value) => (text.Length - text.Replace(value, string.Empty, StringComparison.Ordinal).Length) / value.Length;
}
