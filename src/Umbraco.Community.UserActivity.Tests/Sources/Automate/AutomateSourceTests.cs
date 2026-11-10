using System.Text.RegularExpressions;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Automate;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Tests.Sources.Automate;

/// <summary>Golden rows from S3's shapes (Fixtures/automate), mapped without a database.</summary>
public class AutomateSourceTests
{
    private static readonly Guid Human = Guid.Parse("1f0c0000-0000-4000-8000-0000000000aa");
    private static readonly Guid ServiceAccount = Guid.Parse("9a7e0000-0000-4000-8000-0000000000bb");
    private static readonly DateTime T0 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "automate", name + ".json")).Trim();

    private static AuditRow Row(string? eventDetails, string eventType = "umbraco/automate/umbracoAutomate/createContent", int id = 42)
        => new()
        {
            Id = id,
            PerformingUserId = 7,
            PerformingUserKey = ServiceAccount,
            PerformingDetails = "User \"Automate\"",
            PerformingIp = "203.0.113.7",
            EventDateUtc = T0,
            AffectedUserId = 0,
            AffectedUserKey = null,
            EventType = eventType,
            EventDetails = eventDetails,
        };

    private static ActivityEvent Map(string? details, bool showIp = false, bool withDetails = false)
        => AutomateSource.Map(Row(details), showIp, withDetails) ?? throw new InvalidOperationException("row skipped");

    [Fact]
    public void AutomateRow_HumanIsActor_ServiceAccountIsVia()
    {
        ActivityEvent e = Map(Fixture("manual-run"));

        Assert.Equal(SourceAlias.Automate, e.Source);
        Assert.Equal("audit:42", e.Id);
        Assert.Equal(ActorKind.User, e.Actor.Kind);
        Assert.Equal(Human, e.Actor.UserKey);
        Assert.NotNull(e.Actor.Via);
        Assert.Equal(ActorKind.ServiceAccount, e.Actor.Via!.Kind);
        Assert.Equal(ServiceAccount, e.Actor.Via.UserKey);
        Assert.Equal(7, e.Actor.Via.UserId);
        Assert.Equal(Categories.Ran, e.Category);
        Assert.Equal("umbraco/automate/umbracoAutomate/createContent", e.RawType);
        Assert.Equal("automation", e.Entity!.Type);
        Assert.Equal("6f1c2a10-0000-4000-8000-000000000001", e.Entity.Key);
        Assert.Equal("createContent step ran", e.Summary);
        Assert.Equal(new DateTimeOffset(T0), e.OccurredUtc);
    }

    [Theory]
    [InlineData("scheduled", "Automate (scheduled)")]
    [InlineData("replay", "Automate (replay)")]
    public void Scheduled_IsAutomationKind(string fixture, string label)
    {
        ActivityEvent e = Map(Fixture(fixture));

        Assert.Equal(ActorKind.Automation, e.Actor.Kind);
        Assert.Null(e.Actor.UserKey);
        Assert.Null(e.Actor.UserId);
        Assert.Equal(label, e.Actor.StoredName);
        Assert.Equal(ServiceAccount, e.Actor.Via!.UserKey);
    }

    [Fact]
    public void Replay_RunIdIsNeverAUser()
    {
        // The replay fixture's id is the human key on purpose: only "user:" names a person (S3).
        ActivityEvent e = Map(Fixture("replay"));

        Assert.NotEqual(Human, e.Actor.UserKey);
        Assert.False(Involves(e, Human));
    }

    [Theory]
    [InlineData("webhook:abc", "Automate (webhook)")]
    [InlineData("ai-agent:x", "Automate (ai-agent)")]
    [InlineData("system", "Automate (system)")]
    [InlineData("Some Weird Kind!:x", "Automate")]
    public void OtherInitiators_AreAutomation(string initiatedBy, string label)
    {
        ActivityEvent e = Map($"{{\"automationId\":\"6f1c2a10-0000-4000-8000-000000000001\",\"initiatedBy\":\"{initiatedBy}\"}}");

        Assert.Equal(ActorKind.Automation, e.Actor.Kind);
        Assert.Equal(label, e.Actor.StoredName);
    }

    [Theory]
    [InlineData("unknown-shape")]
    [InlineData(null)]
    public void UnknownDetailsShape_FallsBackToPerformer_Flagged(string? fixture)
    {
        string? details = fixture is null ? "not json at all" : Fixture(fixture);

        ActivityEvent e = Map(details, withDetails: true);

        Assert.Equal(ActorKind.ServiceAccount, e.Actor.Kind);
        Assert.Equal(ServiceAccount, e.Actor.UserKey);
        Assert.Null(e.Actor.Via);
        Assert.Equal("performer", e.Details!["actorFallback"]);
        Assert.Null(e.Entity);
    }

    [Theory]
    [InlineData("user:not-a-guid")]
    [InlineData("user:")]
    public void UserInitiatorWithoutAGuid_FallsBackToPerformer(string initiatedBy)
    {
        ActivityEvent e = Map($"{{\"initiatedBy\":\"{initiatedBy}\"}}", withDetails: true);

        Assert.Equal(ActorKind.ServiceAccount, e.Actor.Kind);
        Assert.Equal("performer", e.Details!["actorFallback"]);
    }

    [Fact]
    public void DetailsDepthOver16_Skipped()
        => Assert.Null(AutomateSource.Map(Row(Fixture("too-deep")), showIp: false, withDetails: false));

    [Fact]
    public void Details_NeverHoldAnIp_AndIpOnlyWhenShown()
    {
        ActivityEvent hidden = Map(Fixture("manual-run"), withDetails: true);
        ActivityEvent shown = Map(Fixture("manual-run"), showIp: true, withDetails: true);

        Assert.Null(hidden.IpAddress);
        Assert.Equal("203.0.113.7", shown.IpAddress);
        Regex ip = new(@"\b\d{1,3}(\.\d{1,3}){3}\b");
        Assert.DoesNotContain(shown.Details!.Values, v => ip.IsMatch(v));
        Assert.Equal("6f1c2a10-0000-4000-8000-000000000002", shown.Details!["runId"]);
        Assert.Equal("user:1f0c0000-0000-4000-8000-0000000000aa", shown.Details["initiatedBy"]);
    }

    [Fact]
    public void ListRows_CarryNoDetails()
        => Assert.Null(Map(Fixture("manual-run")).Details);

    [Fact]
    public void ClaimsAutomatePrefix()
    {
        AutomateSource source = Source(installed: true);

        Assert.Equal(["umbraco/automate/"], source.ClaimedAuditEventTypePrefixes);
        Assert.Empty(source.ClaimedLogEntityTypes);
        Assert.Equal(SourceAlias.Automate, source.Alias);
        Assert.Equal("userActivity_sourceAutomate", source.DisplayNameKey);
        Assert.Equal([Categories.Ran], source.Info.Categories);
        Assert.Equal(["automation"], source.Info.EntityTypes);
    }

    [Fact]
    public void NotInstalled_Hidden()
    {
        Assert.False(Source(installed: false).IsAvailable);
        Assert.True(Source(installed: true).IsAvailable);
    }

    [Fact]
    public void Installed_ChecksTheAutomateAssemblyByName()
    {
        string? asked = null;
        _ = new AutomateSource(null!, null!, name => { asked = name; return false; }).IsAvailable;

        Assert.Equal("Umbraco.Automate.Core", asked);
    }

    [Fact]
    public void ClaimIsAccepted_ByTheRegistryValidator()
        => SourceRegistryValidator.Validate([Source(installed: true)]);

    [Fact]
    public void AutomationLabel_NeverMatchesAUsername()
    {
        var actor = new ActivityActor(ActorKind.Automation, null, null, "Automate (scheduled)", null);
        var user = new ResolvedUser(Guid.NewGuid(), 99, "Automate (scheduled)", null, null);

        Assert.False(Umbraco.Community.UserActivity.Query.ActivityQueryEngine.Involves(actor, user));
    }

    private static AutomateSource Source(bool installed) => new(null!, null!, _ => installed);

    private static bool Involves(ActivityEvent e, Guid key)
        => Umbraco.Community.UserActivity.Query.ActivityQueryEngine.Involves(e.Actor, new ResolvedUser(key, 99, "sam", null, null));
}
