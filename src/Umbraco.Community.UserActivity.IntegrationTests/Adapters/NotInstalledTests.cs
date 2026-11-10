using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.IntegrationTests.Adapters;

/// <summary>
/// Review Focus 4: a real host with none of the optional integrations installed (this project references none, and
/// the package's compile references never flow to it). Start-up completes, controllers are discovered, and the
/// adapter sources are simply absent.
/// </summary>
[TestFixture]
public class NotInstalledTests : ApiTestBase
{
    private static readonly string[] OptionalAssemblies = ["Umbraco.AI.Core", "uSync.History", "Umbraco.Automate.Core", "Umbraco.Forms.Core"];

    [Test]
    public void NoOptionalAssembly_IsLoaded()
        => Assert.That(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name), Has.None.AnyOf(OptionalAssemblies));

    [Test]
    public async Task NotInstalled_BootsAndHides()
    {
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        HttpClient client = await CreateAuthenticatedClientAsync(admin);

        HttpResponseMessage response = await client.GetAsync(ManagementApi + "user-activity/filter-options");
        string text = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), text);

        using JsonDocument doc = JsonDocument.Parse(text);
        var aliases = doc.RootElement.GetProperty("sources").EnumerateArray().Select(s => s.GetProperty("alias").GetString()).ToList();
        Assert.That(aliases, Is.EqualTo(new[] { SourceAlias.Core }));

        AdapterRegistrations registrations = GetRequiredService<AdapterRegistrations>();
        Assert.That(registrations.Results.Select(r => (r.Alias, r.State)), Is.SupersetOf(new[] { (SourceAlias.AI, AdapterState.NotInstalled), (SourceAlias.USync, AdapterState.NotInstalled) }));
        Assert.That(GetRequiredService<UserActivitySourceCollection>().Select(s => s.Alias), Has.None.AnyOf(SourceAlias.AI, SourceAlias.USync));
    }

    [Test]
    public async Task NotInstalled_EventsStillWork()
    {
        IUser admin = await Users.CreateAdminAsync("Sam Admin");
        HttpClient client = await CreateAuthenticatedClientAsync(admin);
        string window = $"from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"))}";

        HttpResponseMessage response = await client.GetAsync(ManagementApi + "user-activity/events?" + window);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
    }
}
