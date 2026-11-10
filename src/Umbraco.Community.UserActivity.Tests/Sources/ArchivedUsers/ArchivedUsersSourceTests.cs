using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.ArchivedUsers;
using Umbraco.Community.UserActivity.Sources.Automate;
using Umbraco.Community.UserActivity.Sources.Core;
using Umbraco.Community.UserActivity.Sources.Forms;

namespace Umbraco.Community.UserActivity.Tests.Sources.ArchivedUsers;

/// <summary>
/// Archived Users (01) writes one core <c>umbracoLog</c> row per action through <c>IAuditService</c>: header
/// <c>Custom</c>, entity type <c>User</c>, NodeId = the target user, userId = the performer, comment
/// <c>ArchivedUsers: &lt;Action&gt; (&lt;Source&gt;)</c> (S5, 01's ArchivedUserService).
/// </summary>
public class ArchivedUsersSourceTests
{
    private static LogRow Row(string? comment, int? userId = 5, int nodeId = 9)
        => new()
        {
            Id = 3,
            UserId = userId,
            NodeId = nodeId,
            EntityType = "User",
            Datestamp = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc),
            LogHeader = "Custom",
            LogComment = comment,
        };

    [Theory]
    [InlineData("ArchivedUsers: Archived (Manual)", "Archived", Categories.Other)]
    [InlineData("ArchivedUsers: Unarchived (CoreEnable)", "Unarchived", Categories.Other)]
    [InlineData("ArchivedUsers: AutoArchived (Policy)", "AutoArchived", Categories.Other)]
    [InlineData("ArchivedUsers: AutoDisabled (Policy)", "AutoDisabled", Categories.Other)]
    [InlineData("ArchivedUsers: Exported (Manual)", "Exported", Categories.Exported)]
    [InlineData("ArchivedUsers: AuditPurged (CleanUp)", "AuditPurged", Categories.Deleted)]
    [InlineData("ArchivedUsers: <img src=x onerror=alert(1)> (x)", "ArchivedUsers", Categories.Other)]
    [InlineData("ArchivedUsers:", "ArchivedUsers", Categories.Other)]
    public void Actions_MapToCategories_WithAClearRawType(string comment, string rawType, string category)
    {
        ActivityEvent e = ArchivedUsersSource.Map(Row(comment), withDetails: false);

        Assert.Equal(rawType, e.RawType);
        Assert.Equal(category, e.Category);
        Assert.Equal(comment, e.Summary);
    }

    [Fact]
    public void Identity_PerformerIsActor_TargetIsTheEntity()
    {
        ActivityEvent e = ArchivedUsersSource.Map(Row("ArchivedUsers: Archived (Bulk)"), withDetails: true);

        Assert.Equal(SourceAlias.ArchivedUsers, e.Source);
        Assert.Equal("log:3", e.Id);
        Assert.Equal(new ActivityActor(ActorKind.User, null, 5, null, null), e.Actor);
        Assert.Equal(new ActivityEntity("user", null, "9", null, true), e.Entity);
        Assert.Equal("Bulk", e.Details!["source"]);
        Assert.Equal("9", e.Details["targetUserId"]);
    }

    [Fact]
    public void NoPerformer_IsSystem()
        => Assert.Equal(ActorKind.System, ArchivedUsersSource.Map(Row("ArchivedUsers: AutoArchived (Policy)", userId: null), false).Actor.Kind);

    [Fact]
    public void ClaimsUserCustomRows_WithTheCommentPrefix_Only()
    {
        ArchivedUsersSource source = Source(installed: true);

        Assert.Equal([new ClaimedLogRows("User", "Custom", "ArchivedUsers:")], source.ClaimedLogRows);
        Assert.Empty(source.ClaimedLogEntityTypes);
        Assert.Empty(source.ClaimedAuditEventTypePrefixes);
        Assert.Equal("userActivity_sourceArchivedUsers", source.DisplayNameKey);
        Assert.Equal(["user"], source.Info.EntityTypes);
    }

    [Fact]
    public void NotInstalled_Hidden()
    {
        string? asked = null;
        Assert.False(new ArchivedUsersSource(null!, null!, n => { asked = n; return false; }).IsAvailable);
        Assert.Equal("Umbraco.Community.ArchivedUsers", asked);
        Assert.True(Source(installed: true).IsAvailable);
    }

    [Fact]
    public void Claims_DoNotClash_WithTheOtherBuiltIns()
        => SourceRegistryValidator.Validate([Source(true), new FormsSource(null!, null!, _ => true), new AutomateSource(null!, null!, _ => true)]);

    [Fact]
    public void RowClaim_OverlappingAWholeEntityClaim_Fails()
    {
        // User itself is core-owned (whole-type claims on it fail on their own), so this uses another entity type.
        var partial = new RowClaimSource(new ClaimedLogRows("Dictionary", "Custom", "Vendor:"), "partial");
        var whole = new FakeSource("whole", logEntityTypes: ["dictionary"]);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => SourceRegistryValidator.Validate([partial, whole]));

        Assert.Contains("'partial'", ex.Message);
        Assert.Contains("'whole'", ex.Message);
    }

    [Theory]
    [InlineData("User", "Custom", "ArchivedUsers: Arch")]
    [InlineData("user", "custom", "archivedusers:")]
    [InlineData("User", "Custom", "Archived")]
    public void OverlappingRowClaims_Fail(string entityType, string header, string prefix)
    {
        var other = new RowClaimSource(new ClaimedLogRows(entityType, header, prefix));

        Assert.Throws<InvalidOperationException>(() => SourceRegistryValidator.Validate([Source(true), other]));
    }

    [Theory]
    [InlineData("", "Custom", "x")]
    [InlineData("User", " ", "x")]
    [InlineData("User", "Custom", "")]
    public void BlankRowClaim_Fails(string entityType, string header, string prefix)
        => Assert.Throws<InvalidOperationException>(() => SourceRegistryValidator.Validate([new RowClaimSource(new ClaimedLogRows(entityType, header, prefix))]));

    [Fact]
    public void DifferentHeaderOrEntity_DoesNotClash()
        => SourceRegistryValidator.Validate([Source(true), new RowClaimSource(new ClaimedLogRows("User", "Save", "ArchivedUsers:")), new RowClaimSource(new ClaimedLogRows("Member", "Custom", "ArchivedUsers:"), "rowclaimtwo")]);

    private static ArchivedUsersSource Source(bool installed) => new(null!, null!, _ => installed);

    private sealed class RowClaimSource(ClaimedLogRows claim, string alias = "rowclaim") : FakeSource(alias), IClaimsCoreAuditRows
    {
        public IReadOnlyCollection<ClaimedLogRows> ClaimedLogRows { get; } = [claim];
    }
}
