using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Tests.Sources;

public class ContractTests
{
    [Fact]
    public void Categories_ArePinned()
    {
        // Permanent strings (design 3.4): adding is fine, renaming or removing is a breaking change.
        string[] expected =
        [
            "created", "saved", "published", "unpublished", "deleted", "trashed", "restored", "moved", "copied",
            "sorted", "rolledBack", "signedIn", "signInFailed", "signedOut", "passwordChanged", "passwordReset",
            "permissionsChanged", "imported", "exported", "ran", "other",
        ];

        Assert.Equal(expected, Categories.All);
        Assert.Equal("rolledBack", Categories.RolledBack);
        Assert.Equal("signInFailed", Categories.SignInFailed);
    }

    [Theory]
    [InlineData("core", true)]
    [InlineData("ai", true)]
    [InlineData("usync", true)]
    [InlineData("archivedUsers", true)]
    [InlineData("formsAuditTrail", true)]
    [InlineData("recorder", true)]
    [InlineData("a", false)]
    [InlineData("Core", false)]
    [InlineData("1ai", false)]
    [InlineData("ai-x", false)]
    [InlineData("ai x", false)]
    [InlineData("", false)]
    [InlineData("abcdefghijabcdefghijabcdefghijab", true)]
    [InlineData("abcdefghijabcdefghijabcdefghijabc", false)]
    public void SourceAlias_Pattern(string alias, bool valid)
        => Assert.Equal(valid, SourceAlias.IsValid(alias));

    [Fact]
    public void ActivityEvent_ToString_PrintsSourceAndIdOnly()
    {
        var actor = new ActivityActor(ActorKind.User, Guid.NewGuid(), 42, "Sentinel Person", null);
        var entity = new ActivityEntity("document", null, "1", "Sentinel Item", true);
        var e = new ActivityEvent(
            "core", "log:1", DateTimeOffset.UnixEpoch, actor, Categories.Saved, "Save", entity,
            "Sentinel summary", "203.0.113.9", new Dictionary<string, string> { ["k"] = "Sentinel detail" });

        string text = e.ToString();

        Assert.Contains("core", text);
        Assert.Contains("log:1", text);
        Assert.DoesNotContain("Sentinel", text);
        Assert.DoesNotContain("203.0.113.9", text);
        Assert.DoesNotContain("Sentinel", actor.ToString());
        Assert.DoesNotContain("Sentinel", entity.ToString());
    }

    [Fact]
    public void QueryAndUser_ToString_HoldNoPersonalData()
    {
        var user = new ResolvedUser(Guid.NewGuid(), 42, "sentinel@example.test", "sentinel@example.test", ["sentinel-old"]);
        var query = new ActivitySourceQuery(
            user, new HashSet<string>(), new HashSet<string>(), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            "sentinel search", 50, false);

        Assert.DoesNotContain("sentinel", user.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sentinel", query.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
