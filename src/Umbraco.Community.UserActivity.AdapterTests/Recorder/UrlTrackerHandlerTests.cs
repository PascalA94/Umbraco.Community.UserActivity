#if UMBRACO_17
using NSubstitute;
using UrlTracker.Core.Database.Entities;
using UrlTracker.Core.Notifications;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Recorder.Handlers.UrlTracker;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.AdapterTests.Recorder;

/// <summary>
/// The UrlTracker handlers bound to a real UrlTracker.Core at <c>UrlTrackerRuntimeVersion</c> (M5 Task 32). Line 17 only:
/// UrlTracker has no stable 18.x release, so line 18 has no handler and no test.
/// </summary>
public class UrlTrackerHandlerTests
{
    private const string Sentinel = "SentinelQueryValue";

    private static HandlerHarness Harness(bool withUser = true, Exception? writerFailure = null)
        => HandlerHarness.Create(UrlTrackerGuard.TryRegister, withUser, writerFailure);

    private static IRedirect Redirect(Guid key, string source = "/old-page", bool advanced = false)
    {
        IRedirect redirect = Substitute.For<IRedirect>();
        redirect.Key.Returns(key);
        redirect.SourceValue.Returns(source);
        redirect.TargetValue.Returns("https://target.example.test/?token=" + Sentinel);
        redirect.Permanent.Returns(true);
        redirect.Force.Returns(false);
        redirect.RetainQuery.Returns(true);
        redirect.Advanced.Returns(advanced);
        return redirect;
    }

    private static IRedirect[] One(string source = "/old-page") => [Redirect(Guid.NewGuid(), source)];

    [Fact]
    public void RealAssembly_IsTheRuntimeVersion_AndTheProbePasses()
    {
        var assembly = typeof(RedirectCreatedNotification).Assembly;

        HandlerHarness.AssertIsRuntimeVersion(assembly, "UrlTracker");
        Assert.Equal(AdapterState.Registered, UrlTrackerGuard.Evaluate(assembly).State);
    }

    [Fact]
    public void PassingAssembly_InjectedIntoTheGuard_Registers()
    {
        HandlerHarness harness = HandlerHarness.Create((builder, registry) => UrlTrackerGuard.TryRegister(builder, registry, typeof(RedirectCreatedNotification).Assembly));

        Assert.Equal(AdapterState.Registered, harness.Result.State);
        Assert.Equal([RecorderSources.UrlTracker], harness.Registry.Names);
        Assert.True(harness.Registry.Any);
    }

    [Fact]
    public void Installed_RegistersThreeHandlersAndTheRecorderName()
    {
        HandlerHarness harness = Harness();

        Assert.Equal(AdapterState.Registered, harness.Result.State);
        Assert.Equal([RecorderSources.UrlTracker], harness.Registry.Names);
        Assert.Equal(1, harness.HandlerCount<RedirectCreatedNotification>());
        Assert.Equal(1, harness.HandlerCount<RedirectUpdatedNotification>());
        Assert.Equal(1, harness.HandlerCount<RedirectDeletedNotification>());
    }

    [Fact]
    public void Created_Updated_AndDeleted_AreExactlyOneRowEach_WithTheBackOfficeUser()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new RedirectCreatedNotification(One()));
        harness.Publish(new RedirectUpdatedNotification(One()));
        harness.Publish(new RedirectDeletedNotification(One()));

        Assert.Equal(
            [(Categories.Created, nameof(RedirectCreatedNotification)), (Categories.Saved, nameof(RedirectUpdatedNotification)), (Categories.Deleted, nameof(RedirectDeletedNotification))],
            harness.Written.Select(r => (r.Category, r.RawType)));
        Assert.All(harness.Written, row =>
        {
            Assert.Equal(RecorderSources.UrlTracker, row.Source);
            Assert.Equal("redirect", row.EntityType);
            Assert.Equal("/old-page", row.EntityName);
            Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
            Assert.Equal("Wren Writer", row.UserName);
        });
    }

    [Fact]
    public void Deleted_SeveralRedirects_OneRowEach_CappedPerNotification()
    {
        HandlerHarness harness = Harness();
        IRedirect[] many = Enumerable.Range(0, UrlTrackerHandlers.MaxRowsPerNotification + 25).Select(i => Redirect(Guid.NewGuid(), "/r" + i)).ToArray();

        harness.Publish(new RedirectDeletedNotification(many));

        // The cap still holds for redirect rows; the one extra row is the truncated marker (asserted below).
        List<RecorderEntry> redirects = harness.Written.Where(r => r.EntityKey is not null).ToList();
        Assert.Equal(UrlTrackerHandlers.MaxRowsPerNotification, redirects.Count);
        Assert.Equal(redirects.Count, redirects.Select(r => r.EntityKey).Distinct().Count());
    }

    [Fact]
    public void Deleted_OverTheCap_WritesOneTruncatedRowWithTheDroppedCount()
    {
        HandlerHarness harness = Harness();
        IRedirect[] many = Enumerable.Range(0, UrlTrackerHandlers.MaxRowsPerNotification + 25).Select(i => Redirect(Guid.NewGuid(), "/r" + i)).ToArray();

        harness.Publish(new RedirectDeletedNotification(many));

        Assert.Equal(UrlTrackerHandlers.MaxRowsPerNotification + 1, harness.Written.Count);
        RecorderEntry marker = harness.Written[^1];
        HandlerHarness.AssertOnlyKeys(marker, "truncated");
        Assert.Equal("25", marker.Details!["truncated"]);
        Assert.Equal(Categories.Deleted, marker.Category);
        Assert.Equal(nameof(RedirectDeletedNotification), marker.RawType);
        Assert.Equal(HandlerHarness.WrenKey, marker.UserKey);
    }

    [Fact]
    public void Deleted_AtTheCap_WritesNoTruncatedRow()
    {
        HandlerHarness harness = Harness();
        IRedirect[] exact = Enumerable.Range(0, UrlTrackerHandlers.MaxRowsPerNotification).Select(i => Redirect(Guid.NewGuid(), "/r" + i)).ToArray();

        harness.Publish(new RedirectDeletedNotification(exact));

        Assert.Equal(UrlTrackerHandlers.MaxRowsPerNotification, harness.Written.Count);
        Assert.DoesNotContain(harness.Written, r => r.Details!.ContainsKey("truncated"));
    }

    [Fact]
    public void Created_AdvancedRedirect_StoresAFixedLabel_NeverTheRegexSource()
    {
        HandlerHarness harness = Harness();
        // \x3F is a regex-escaped question mark: no literal "?" for PathOnly to cut at, yet the token is in the pattern.
        IRedirect advanced = Redirect(Guid.NewGuid(), @"^/old\x3Ftoken=" + Sentinel + "$", advanced: true);

        harness.Publish(new RedirectCreatedNotification([advanced]));

        RecorderEntry row = Assert.Single(harness.Written);
        Assert.Equal("Pattern redirect", row.EntityName);
        Assert.Equal("true", row.Details!["advanced"]);
        HandlerHarness.AssertNoTrace(row, Sentinel);
    }

    [Fact]
    public void Created_NoBackOfficeUser_StoresNullUserKey()
    {
        HandlerHarness harness = Harness(withUser: false);

        harness.Publish(new RedirectCreatedNotification(One()));

        Assert.Null(Assert.Single(harness.Written).UserKey);
    }

    [Fact]
    public void Created_ThrowingWriter_NeverThrowsIntoUrlTrackersSave()
    {
        HandlerHarness harness = Harness(writerFailure: new InvalidOperationException(Sentinel));

        Exception? thrown = Record.Exception(() =>
        {
            harness.Publish(new RedirectCreatedNotification(One()));
            harness.Publish(new RedirectUpdatedNotification(One()));
            harness.Publish(new RedirectDeletedNotification(One()));
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void Created_DetailsHoldOnlyAllowListedKeys_NeverTheQueryStringOrTarget()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new RedirectCreatedNotification(One("/old-page?utm=" + Sentinel)));

        RecorderEntry row = Assert.Single(harness.Written);
        HandlerHarness.AssertOnlyKeys(row, "permanent", "force", "retainQuery", "advanced");
        Assert.Equal("true", row.Details!["permanent"]);
        Assert.Equal("/old-page", row.EntityName);
        HandlerHarness.AssertNoTrace(row, Sentinel);
        HandlerHarness.AssertNoTrace(row, "target.example.test");
    }
}
#endif
