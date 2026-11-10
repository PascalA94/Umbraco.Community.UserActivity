using System.Net;
using System.Text.Json;

namespace Umbraco.Community.UserActivity.IntegrationTests.Api;

/// <summary>The "user-activity" document exists on both lines with the permanent operation ids (plan Task 12).</summary>
[TestFixture]
public class OpenApiDocumentTests : ApiTestBase
{
#if UMBRACO_17
    private const string DocumentUrl = "/umbraco/swagger/user-activity/swagger.json";
#else
    private const string DocumentUrl = "/umbraco/openapi/user-activity.json";
#endif

    [Test]
    public async Task Document_HasPermanentOperationIds_AndBackOfficeAuth()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(DocumentUrl);
        string text = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), text.Length > 300 ? text[..300] : text);

        using JsonDocument doc = JsonDocument.Parse(text);
        var operations = doc.RootElement.GetProperty("paths").EnumerateObject()
            .SelectMany(p => p.Value.EnumerateObject().Select(o => (Path: p.Name, Method: o.Name, Op: o.Value)))
            .ToList();

        Assert.That(operations.Select(o => o.Op.GetProperty("operationId").GetString()), Is.EquivalentTo(new[]
        {
            "GetActivityEvents", "GetUserActivityEvents", "GetActivityEvent", "GetActivityFilterOptions", "ExportActivityEvents",
        }));
        Assert.That(operations.All(o => o.Method == "get"), Is.True);
        Assert.That(operations.All(o => o.Path.StartsWith("/umbraco/management/api/v1/user-activity/", StringComparison.Ordinal)), Is.True);
        Assert.That(operations.All(o => o.Op.TryGetProperty("security", out _)), Is.True, "every operation requires backoffice auth");
    }
}
