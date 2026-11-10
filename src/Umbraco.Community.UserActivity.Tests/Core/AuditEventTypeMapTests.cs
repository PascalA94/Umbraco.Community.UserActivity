using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Core;

namespace Umbraco.Community.UserActivity.Tests.Core;

public class AuditEventTypeMapTests
{
    // Strings read from the 17.8.0 and 18.3.0 CMS assemblies (identical on both lines) and S5.
    [Theory]
    [InlineData("umbraco/user/sign-in/login", Categories.SignedIn, "user")]
    [InlineData("umbraco/user/sign-in/logout", Categories.SignedOut, "user")]
    [InlineData("umbraco/user/sign-in/failed", Categories.SignInFailed, "user")]
    [InlineData("umbraco/user/password/reset", Categories.PasswordReset, "user")]
    [InlineData("umbraco/user/password/change", Categories.PasswordChanged, "user")]
    [InlineData("umbraco/user/password/forgot/change", Categories.PasswordReset, "user")]
    [InlineData("umbraco/user/password/forgot/request", Categories.Other, "user")]
    [InlineData("umbraco/user/save", Categories.Saved, "user")]
    [InlineData("umbraco/user/delete", Categories.Deleted, "user")]
    [InlineData("umbraco/user-group/save", Categories.Saved, "userGroup")]
    [InlineData("umbraco/user-group/permissions-change", Categories.PermissionsChanged, "userGroup")]
    [InlineData("umbraco/member/save", Categories.Saved, "member")]
    [InlineData("umbraco/member/delete", Categories.Deleted, "member")]
    [InlineData("umbraco/member/exported", Categories.Exported, "member")]
    [InlineData("umbraco/member/roles/assigned", Categories.PermissionsChanged, "member")]
    [InlineData("community/useractivity/viewed", Categories.Other, "user")]
    [InlineData("community/useractivity/exported", Categories.Exported, "user")]
    [InlineData("someone/else/thing", Categories.Other, null)]
    public void EventTypeMap_FromS5(string eventType, string category, string? entityType)
    {
        Assert.Equal(category, AuditEventTypeMap.ToCategory(eventType));
        Assert.Equal(entityType, AuditEventTypeMap.ToEntityType(eventType));
    }

    [Fact]
    public void EventTypesFor_ReversesTheMap()
    {
        Assert.Equal(["umbraco/user/sign-in/login"], AuditEventTypeMap.EventTypesFor(Categories.SignedIn));
        Assert.Empty(AuditEventTypeMap.EventTypesFor(Categories.Published));
    }
}
