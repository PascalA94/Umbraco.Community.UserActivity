using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.IntegrationTests.Api;

/// <summary>QA-3: an export never silently leaves out a source that failed.</summary>
[TestFixture]
public class ExportFailedSourceTests : ApiTestBase
{
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.UserActivitySources().Append<BrokenSource>();
    }

    [Test]
    public async Task Export_SourceFailed_503_NoFile_NoAuditRow()
    {
        Services.GetRequiredService<IOptionsMonitor<UserActivityOptions>>().CurrentValue.Export.Enabled = true;
        HttpClient client = await CreateAuthenticatedClientAsync(await Users.CreateAdminAsync());

        HttpResponseMessage export = await client.GetAsync(ManagementApi + "user-activity/events/export");
        string list = await client.GetStringAsync(ManagementApi + "user-activity/events?category=exported");

        Assert.That(export.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await export.Content.ReadAsStringAsync(), Does.Contain("broken"));
        Assert.That(list, Does.Not.Contain("community/useractivity/exported"));
        Assert.That(list, Does.Contain("\"partial\"").Or.Contain("failed"), "the list view still answers, with the source marked failed");
    }

    public sealed class BrokenSource : IUserActivitySource
    {
        public string Alias => "broken";

        public string DisplayNameKey => "userActivity_sourceBroken";

        public bool IsAvailable => true;

        public ActivitySourceInfo Info { get; } = new([], Categories.All, true, true, null);

        public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct) => throw new InvalidOperationException();

        public Task<ActivityEvent?> GetAsync(string id, CancellationToken ct) => throw new InvalidOperationException();
    }
}
