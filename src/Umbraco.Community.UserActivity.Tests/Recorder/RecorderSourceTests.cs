using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Tests.Recorder;

public class RecorderSourceTests
{
    private static ActivityEventDto Row(Guid? userKey, string? userName = "Wren Writer", string? details = "{\"mode\":\"report\"}")
        => new()
        {
            Id = 42,
            OccurredUtc = new DateTime(2026, 11, 9, 8, 30, 0, DateTimeKind.Unspecified),
            Source = "csp",
            Category = Categories.Saved,
            RawType = "CspSavedNotification",
            UserKey = userKey,
            UserName = userName,
            EntityType = "cspPolicy",
            EntityKey = "e-1",
            EntityName = "Main policy",
            DetailsJson = details,
        };

    private static RecorderSource Source(int retentionDays = 0, bool handlers = true)
    {
        var registry = new RecorderHandlerRegistry();
        if (handlers)
        {
            registry.Register("csp");
        }

        return new RecorderSource(null!, new FixedOptions(new UserActivityOptions { Recorder = new RecorderOptions { RetentionDays = retentionDays } }), registry);
    }

    [Fact]
    public void RecorderSource_MapsRows()
    {
        var userKey = Guid.NewGuid();

        ActivityEvent e = RecorderSource.Map(Row(userKey), withDetails: true);

        Assert.Equal(SourceAlias.Recorder, e.Source);
        Assert.Equal("rec:42", e.Id);
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 8, 30, 0, TimeSpan.Zero), e.OccurredUtc);
        Assert.Equal(new ActivityActor(ActorKind.User, userKey, null, "Wren Writer", null), e.Actor);
        Assert.Equal(Categories.Saved, e.Category);
        Assert.Equal("CspSavedNotification", e.RawType);
        Assert.Equal(new ActivityEntity("cspPolicy", "e-1", null, "Main policy", true), e.Entity);
        Assert.Null(e.IpAddress);
        Assert.Equal("report", e.Details!["mode"]);
        Assert.Equal("csp", e.Details["package"]);
    }

    [Fact]
    public void RecorderSource_DetailsAreReadersOnly_ListRowsCarryNone()
        => Assert.Null(RecorderSource.Map(Row(Guid.NewGuid()), withDetails: false).Details);

    [Fact]
    public void RecorderSource_NoBackofficeUser_IsSystem()
    {
        ActivityEvent e = RecorderSource.Map(Row(null, userName: null), withDetails: false);

        Assert.Equal(new ActivityActor(ActorKind.System, null, null, null, null), e.Actor);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"a\":{\"nested\":1},\"b\":\"ok\"}")]
    public void RecorderSource_MalformedDetails_NeverThrows_KeepsStringValuesOnly(string json)
    {
        ActivityEvent e = RecorderSource.Map(Row(Guid.NewGuid(), details: json), withDetails: true);

        Assert.DoesNotContain("a", e.Details!.Keys);
        Assert.Equal("csp", e.Details["package"]);
    }

    [Fact]
    public void DeletedUser_FallsBackToStoredName()
    {
        // The row keeps the name at the time (design 4.1), so once the user is gone the engine falls back to it; with
        // no stored name the viewer gets the Unknown-user label, never another person's name.
        var gone = Guid.NewGuid();
        var resolved = new Dictionary<ActorRef, ActorDisplay> { [ActorRef.ForKey(gone)] = new(ActorKind.Unknown, gone, null, null, ActorDisplay.UnknownLabel(gone, null)) };

        ActorDisplay withName = ActorDisplay.Compose(RecorderSource.Map(Row(gone, "Wren Writer"), false).Actor, resolved);
        ActorDisplay withoutName = ActorDisplay.Compose(RecorderSource.Map(Row(gone, userName: null), false).Actor, resolved);

        Assert.Equal(ActorKind.Unknown, withName.Kind);
        Assert.Equal("Wren Writer", withName.Name);
        Assert.Null(withoutName.Name);
        Assert.StartsWith("Unknown user", withoutName.Label);
    }

    [Theory]
    [InlineData(0, true, null)]
    [InlineData(30, false, 30)]
    [InlineData(365, false, 365)]
    public void RecorderHint_KeptForeverWhenZero(int retentionDays, bool keptForever, int? hintDays)
    {
        ActivitySourceInfo info = Source(retentionDays).Info;

        Assert.Equal(keptForever, info.KeptForever);
        Assert.Equal(hintDays is { } d ? TimeSpan.FromDays(d) : null, info.RetentionHint);
    }

    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(36501)]
    [InlineData(-5)]
    public void RecorderHint_BadValue_NeverThrows(int retentionDays)
    {
        ActivitySourceInfo info = Source(retentionDays).Info;

        Assert.Equal(retentionDays == 0, info.KeptForever);
    }

    [Fact]
    public void RecorderSource_HiddenUntilAHandlerIsRegistered()
    {
        Assert.False(Source(handlers: false).IsAvailable);
        Assert.True(Source(handlers: true).IsAvailable);
    }

    [Fact]
    public void RecorderSource_Contract()
    {
        RecorderSource source = Source();

        Assert.Equal("recorder", source.Alias);
        Assert.Equal("userActivity_sourceRecorder", source.DisplayNameKey);
        Assert.True(source.Info.CanCount);
        Assert.True(source.Info.CanSearch);
        Assert.True(SourceAlias.IsValid(source.Alias));
        Assert.All(source.Info.Categories, c => Assert.True(Categories.IsKnown(c)));
    }

    [Fact]
    public void RecorderSource_ClaimsNothingFromCore()
        => SourceRegistryValidator.Validate([Source()]);

    private sealed class FixedOptions(UserActivityOptions value) : Microsoft.Extensions.Options.IOptionsMonitor<UserActivityOptions>
    {
        public UserActivityOptions CurrentValue => value;

        public UserActivityOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<UserActivityOptions, string?> listener) => null;
    }
}
