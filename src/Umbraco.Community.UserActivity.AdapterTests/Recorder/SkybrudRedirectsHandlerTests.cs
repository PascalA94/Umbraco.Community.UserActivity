using NSubstitute;
using Skybrud.Umbraco.Redirects.Models;
using Skybrud.Umbraco.Redirects.Notifications;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Recorder.Handlers.SkybrudRedirects;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.AdapterTests.Recorder;

/// <summary>The Skybrud Redirects handlers bound to a real Skybrud.Umbraco.Redirects at <c>SkybrudRedirectsRuntimeVersion</c> (M5 Task 32).</summary>
public class SkybrudRedirectsHandlerTests
{
    private const string Sentinel = "SentinelQueryValue";
    private static readonly Guid RedirectKey = Guid.Parse("4d0c0000-0000-4000-8000-0000000000dd");

    private static HandlerHarness Harness(bool withUser = true, Exception? writerFailure = null)
        => HandlerHarness.Create(SkybrudRedirectsGuard.TryRegister, withUser, writerFailure);

    private static IRedirect Redirect(string path = "/old-page")
    {
        IRedirect redirect = Substitute.For<IRedirect>();
        redirect.Key.Returns(RedirectKey);
        redirect.Path.Returns(path);
        redirect.QueryString.Returns("token=" + Sentinel);
        redirect.Url.Returns(path + "?token=" + Sentinel);
        redirect.IsPermanent.Returns(true);
        redirect.Type.Returns(RedirectType.Permanent);
        return redirect;
    }

    [Fact]
    public void RealAssembly_IsTheRuntimeVersion_AndTheProbePasses()
    {
        var assembly = typeof(RedirectAddedNotification).Assembly;

        HandlerHarness.AssertIsRuntimeVersion(assembly, "SkybrudRedirects");
        Assert.Equal(AdapterState.Registered, SkybrudRedirectsGuard.Evaluate(assembly).State);
    }

    [Fact]
    public void PassingAssembly_InjectedIntoTheGuard_Registers()
    {
        HandlerHarness harness = HandlerHarness.Create((builder, registry) => SkybrudRedirectsGuard.TryRegister(builder, registry, typeof(RedirectAddedNotification).Assembly));

        Assert.Equal(AdapterState.Registered, harness.Result.State);
        Assert.Equal([RecorderSources.SkybrudRedirects], harness.Registry.Names);
        Assert.True(harness.Registry.Any);
    }

    [Fact]
    public void Installed_RegistersThreeHandlersAndTheRecorderName()
    {
        HandlerHarness harness = Harness();

        Assert.Equal(AdapterState.Registered, harness.Result.State);
        Assert.Equal([RecorderSources.SkybrudRedirects], harness.Registry.Names);
        Assert.Equal(1, harness.HandlerCount<RedirectAddedNotification>());
        Assert.Equal(1, harness.HandlerCount<RedirectSavedNotification>());
        Assert.Equal(1, harness.HandlerCount<RedirectDeletedNotification>());
    }

    [Fact]
    public void Added_Saved_AndDeleted_AreExactlyOneRowEach_WithTheBackOfficeUser()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new RedirectAddedNotification(Redirect()));
        harness.Publish(new RedirectSavedNotification(Redirect()));
        harness.Publish(new RedirectDeletedNotification(Redirect()));

        Assert.Equal(
            [(Categories.Created, nameof(RedirectAddedNotification)), (Categories.Saved, nameof(RedirectSavedNotification)), (Categories.Deleted, nameof(RedirectDeletedNotification))],
            harness.Written.Select(r => (r.Category, r.RawType)));
        Assert.All(harness.Written, row =>
        {
            Assert.Equal(RecorderSources.SkybrudRedirects, row.Source);
            Assert.Equal("redirect", row.EntityType);
            Assert.Equal(RedirectKey.ToString(), row.EntityKey);
            Assert.Equal("/old-page", row.EntityName);
            Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
            Assert.Equal("Wren Writer", row.UserName);
        });
    }

    [Fact]
    public void Deleted_NoBackOfficeUser_StoresNullUserKey()
    {
        HandlerHarness harness = Harness(withUser: false);

        harness.Publish(new RedirectDeletedNotification(Redirect()));

        Assert.Null(Assert.Single(harness.Written).UserKey);
    }

    [Fact]
    public void Added_ThrowingWriter_NeverThrowsIntoSkybrudsSave()
    {
        HandlerHarness harness = Harness(writerFailure: new InvalidOperationException(Sentinel));

        Exception? thrown = Record.Exception(() =>
        {
            harness.Publish(new RedirectAddedNotification(Redirect()));
            harness.Publish(new RedirectSavedNotification(Redirect()));
            harness.Publish(new RedirectDeletedNotification(Redirect()));
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void Added_RedirectThatThrowsWhenRead_NeverThrowsIntoSkybrudsSave()
    {
        HandlerHarness harness = Harness();
        IRedirect broken = Substitute.For<IRedirect>();
        broken.Key.Returns(_ => throw new InvalidOperationException(Sentinel));

        Exception? thrown = Record.Exception(() => harness.Publish(new RedirectAddedNotification(broken)));

        Assert.Null(thrown);
        Assert.Empty(harness.Written);
    }

    [Fact]
    public void Added_DetailsHoldOnlyAllowListedKeys_NeverTheQueryStringOrDestination()
    {
        HandlerHarness harness = Harness();
        IRedirect redirect = Redirect("/old-page?token=" + Sentinel + "#frag");
        IRedirectDestination destination = Substitute.For<IRedirectDestination>();
        destination.Url.Returns("https://destination.example.test/?q=" + Sentinel);
        redirect.Destination.Returns(destination);

        harness.Publish(new RedirectAddedNotification(redirect));

        RecorderEntry row = Assert.Single(harness.Written);
        HandlerHarness.AssertOnlyKeys(row, "type", "permanent");
        Assert.Equal("true", row.Details!["permanent"]);
        Assert.Equal("/old-page", row.EntityName);
        HandlerHarness.AssertNoTrace(row, Sentinel);
        HandlerHarness.AssertNoTrace(row, "destination.example.test");
    }
}
