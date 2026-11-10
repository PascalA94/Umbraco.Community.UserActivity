using Umbraco.Community.CSPManager.Models;
using Umbraco.Community.CSPManager.Notifications;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Recorder.Handlers.CspManager;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.AdapterTests.Recorder;

/// <summary>The CSP Manager handler bound to a real CSP Manager at <c>CspManagerRuntimeVersion</c> (design 4.1, M5 Task 32).</summary>
public class CspManagerHandlerTests
{
    private const string Sentinel = "SentinelPolicyText";
    private static readonly Guid PolicyId = Guid.Parse("3c0c0000-0000-4000-8000-0000000000cc");

    private static HandlerHarness Harness(bool withUser = true, Exception? writerFailure = null)
        => HandlerHarness.Create(CspManagerGuard.TryRegister, withUser, writerFailure);

    private static CspSavedNotification Saved(bool backOffice = false)
        => new(new CspDefinition
        {
            Id = PolicyId,
            Enabled = true,
            ReportOnly = true,
            IsBackOffice = backOffice,
            ReportUri = "https://reports.example.test/" + Sentinel,
            ReportingDirective = Sentinel,
            Sources =
            [
                new CspDefinitionSource { Source = "https://" + Sentinel + ".example.test", Directives = ["script-src", Sentinel] },
                new CspDefinitionSource { Source = "'self'", Directives = ["default-src"] },
            ],
        });

    [Fact]
    public void RealAssembly_IsTheRuntimeVersion_AndTheProbePasses()
    {
        var assembly = typeof(CspSavedNotification).Assembly;

        HandlerHarness.AssertIsRuntimeVersion(assembly, "CspManager");
        Assert.Equal(AdapterState.Registered, CspManagerGuard.Evaluate(assembly).State);
    }

    [Fact]
    public void PassingAssembly_InjectedIntoTheGuard_Registers()
    {
        HandlerHarness harness = HandlerHarness.Create((builder, registry) => CspManagerGuard.TryRegister(builder, registry, typeof(CspSavedNotification).Assembly));

        Assert.Equal(AdapterState.Registered, harness.Result.State);
        Assert.Equal([RecorderSources.Csp], harness.Registry.Names);
        Assert.True(harness.Registry.Any);
    }

    [Fact]
    public void Installed_RegistersTheHandlerAndTheRecorderName()
    {
        HandlerHarness harness = Harness();

        Assert.Equal(AdapterState.Registered, harness.Result.State);
        Assert.Equal([RecorderSources.Csp], harness.Registry.Names);
        Assert.Equal(1, harness.HandlerCount<CspSavedNotification>());
    }

    [Fact]
    public void Saved_IsExactlyOneRow_WithTheBackOfficeUser()
    {
        HandlerHarness harness = Harness();

        harness.Publish(Saved());

        RecorderEntry row = Assert.Single(harness.Written);
        Assert.Equal(RecorderSources.Csp, row.Source);
        Assert.Equal(Categories.Saved, row.Category);
        Assert.Equal(nameof(CspSavedNotification), row.RawType);
        Assert.Equal("cspPolicy", row.EntityType);
        Assert.Equal(PolicyId.ToString(), row.EntityKey);
        Assert.Equal("Front end policy", row.EntityName);
        Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
        Assert.Equal("Wren Writer", row.UserName);
    }

    [Fact]
    public void Saved_BackOfficePolicy_IsNamedAsSuch()
    {
        HandlerHarness harness = Harness();

        harness.Publish(Saved(backOffice: true));

        Assert.Equal("Back office policy", Assert.Single(harness.Written).EntityName);
    }

    [Fact]
    public void Saved_NoBackOfficeUser_StoresNullUserKey()
    {
        HandlerHarness harness = Harness(withUser: false);

        harness.Publish(Saved());

        RecorderEntry row = Assert.Single(harness.Written);
        Assert.Null(row.UserKey);
        Assert.Null(row.UserName);
    }

    [Fact]
    public void Saved_ThrowingWriter_NeverThrowsIntoCspManagersSave()
    {
        HandlerHarness harness = Harness(writerFailure: new InvalidOperationException(Sentinel));

        Exception? thrown = Record.Exception(() => harness.Publish(Saved()));

        Assert.Null(thrown);
    }

    [Fact]
    public void Saved_DefinitionWithoutSources_StillRecords()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new CspSavedNotification(new CspDefinition { Id = PolicyId, Sources = null! }));

        Assert.Equal("0", Assert.Single(harness.Written).Details!["sourceCount"]);
    }

    [Fact]
    public void Saved_DetailsHoldOnlyAllowListedKeys_NeverPolicyText()
    {
        HandlerHarness harness = Harness();

        harness.Publish(Saved());

        RecorderEntry row = Assert.Single(harness.Written);
        HandlerHarness.AssertOnlyKeys(row, "enabled", "reportOnly", "backOffice", "sourceCount");
        Assert.Equal("2", row.Details!["sourceCount"]);
        HandlerHarness.AssertNoTrace(row, Sentinel);
        HandlerHarness.AssertNoTrace(row, "reports.example.test");
        HandlerHarness.AssertNoTrace(row, "script-src");
    }
}
