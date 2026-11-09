using Umbraco.Cms.Core.Models;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Tests.Core;

public class AuditTypeMapTests
{
    [Fact]
    public void AuditTypeMap_EveryAuditType_HasCategory()
    {
        // Runs on both lines: a new AuditType in a CMS minor fails here until it is mapped on purpose.
        string[] unmapped = Enum.GetNames<AuditType>().Where(n => !AuditTypeMap.IsMapped(n)).ToArray();

        Assert.Empty(unmapped);
        Assert.All(Enum.GetNames<AuditType>(), n => Assert.True(Categories.IsKnown(AuditTypeMap.ToCategory(n))));
    }

    [Theory]
    [InlineData("New", Categories.Created)]
    [InlineData("Save", Categories.Saved)]
    [InlineData("SaveVariant", Categories.Saved)]
    [InlineData("Publish", Categories.Published)]
    [InlineData("PublishVariant", Categories.Published)]
    [InlineData("Unpublish", Categories.Unpublished)]
    [InlineData("Delete", Categories.Deleted)]
    [InlineData("Move", Categories.Moved)]
    [InlineData("Copy", Categories.Copied)]
    [InlineData("Sort", Categories.Sorted)]
    [InlineData("RollBack", Categories.RolledBack)]
    [InlineData("PublicAccess", Categories.PermissionsChanged)]
    [InlineData("Custom", Categories.Other)]
    [InlineData("save", Categories.Saved)]
    [InlineData("SomethingNew", Categories.Other)]
    public void AuditTypeMap_Maps(string header, string category)
        => Assert.Equal(category, AuditTypeMap.ToCategory(header));

    [Fact]
    public void AuditTypeMap_HeadersFor_ReversesTheMap()
    {
        Assert.Equal(["publish", "publishvariant"], AuditTypeMap.HeadersFor(Categories.Published).Order());
        Assert.Empty(AuditTypeMap.HeadersFor(Categories.SignedIn));
    }
}
