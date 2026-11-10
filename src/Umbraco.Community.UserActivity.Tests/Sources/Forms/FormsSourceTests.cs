using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Automate;
using Umbraco.Community.UserActivity.Sources.Core;
using Umbraco.Community.UserActivity.Sources.Forms;

namespace Umbraco.Community.UserActivity.Tests.Sources.Forms;

/// <summary>Rows as S2 recorded them: one <c>umbracoLog</c> row per form action, entity type <c>Form</c>.</summary>
public class FormsSourceTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);

    private static LogRow Row(string header, string? comment = null, string? parameters = null, Guid? formKey = null, string? formName = null, int? userId = 5)
        => new()
        {
            Id = 10,
            UserId = userId,
            NodeId = 12,
            EntityType = "Form",
            Datestamp = T0,
            LogHeader = header,
            LogComment = comment,
            Parameters = parameters,
            NodeKey = formKey,
            NodeName = formName,
        };

    [Theory]
    [InlineData("New", null, Categories.Created)]
    [InlineData("Save", null, Categories.Saved)]
    [InlineData("Move", "trash", Categories.Trashed)]
    [InlineData("Move", "restore", Categories.Restored)]
    [InlineData("Move", null, Categories.Moved)]
    [InlineData("RollBack", null, Categories.RolledBack)]
    [InlineData("Delete", null, Categories.Deleted)]
    [InlineData("Custom", null, Categories.Other)]
    public void EachFormsAction_MapsToItsCategory(string header, string? parameters, string category)
        => Assert.Equal(category, FormsSource.Map(Row(header, parameters: parameters), withDetails: false).Category);

    [Fact]
    public void LiveForm_HasItsNameAndKey()
    {
        Guid key = Guid.NewGuid();
        ActivityEvent e = FormsSource.Map(Row("Save", formKey: key, formName: "Contact us"), withDetails: false);

        Assert.Equal(SourceAlias.Forms, e.Source);
        Assert.Equal("log:10", e.Id);
        Assert.Equal(new ActivityEntity("form", key.ToString("D"), "12", "Contact us", true), e.Entity);
        Assert.Equal(ActorKind.User, e.Actor.Kind);
        Assert.Equal(5, e.Actor.UserId);
        Assert.Equal("Save", e.RawType);
        Assert.Null(e.Details);
    }

    [Fact]
    public void DeletedForm_ShowsDeletedFormName()
    {
        ActivityEvent e = FormsSource.Map(Row("Delete"), withDetails: false);

        Assert.Equal(new ActivityEntity("form", null, "12", "Deleted form (id 12)", false), e.Entity);
    }

    [Fact]
    public void FormsTableMissing_NoNameButNotDeleted()
    {
        ActivityEvent e = FormsSource.Map(Row("Save"), withDetails: false, formsTableExists: false);

        Assert.Equal(new ActivityEntity("form", null, "12", null, true), e.Entity);
    }

    [Fact]
    public void Details_HoldTheRawRow_Capped()
    {
        ActivityEvent e = FormsSource.Map(Row("Move", "Moved to recycle bin", "trash"), withDetails: true);

        Assert.Equal("Moved to recycle bin", e.Summary);
        Assert.Equal("Move", e.Details!["logHeader"]);
        Assert.Equal("trash", e.Details["parameters"]);
        Assert.Equal("12", e.Details["nodeId"]);
    }

    [Fact]
    public void NullUser_IsSystem()
        => Assert.Equal(ActorKind.System, FormsSource.Map(Row("Save", userId: null), withDetails: false).Actor.Kind);

    [Fact]
    public void ClaimsExactlyTheFormEntityType()
    {
        FormsSource source = Source(installed: true);

        Assert.Equal(["Form"], source.ClaimedLogEntityTypes);
        Assert.Empty(source.ClaimedAuditEventTypePrefixes);
        Assert.Equal(SourceAlias.Forms, source.Alias);
        Assert.Equal("userActivity_sourceForms", source.DisplayNameKey);
        Assert.Equal(["form"], source.Info.EntityTypes);
    }

    [Fact]
    public void NotInstalled_Hidden()
    {
        string? asked = null;
        Assert.False(new FormsSource(null!, null!, n => { asked = n; return false; }).IsAvailable);
        Assert.Equal("Umbraco.Forms.Core", asked);
        Assert.True(Source(installed: true).IsAvailable);
    }

    [Fact]
    public void Claims_DoNotClash_WithAutomate()
        => SourceRegistryValidator.Validate([Source(installed: true), new AutomateSource(null!, null!, _ => true)]);

    private static FormsSource Source(bool installed) => new(null!, null!, _ => installed);
}
