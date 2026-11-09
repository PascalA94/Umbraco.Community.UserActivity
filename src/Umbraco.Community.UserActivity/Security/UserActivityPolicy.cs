using Microsoft.AspNetCore.Authorization;

namespace Umbraco.Community.UserActivity.Security;

/// <summary>Policy names and requirements (design 5, 7.2). The rules themselves live in <see cref="UserActivityAuthorizationHandler"/>.</summary>
public static class UserActivityPolicy
{
    /// <summary>Everyone's activity: a user in <c>AllowedUserGroups</c> with access to the Users section.</summary>
    public const string Read = "UserActivity.Read";

    /// <summary>The outer gate for the per-user, detail and filter-option routes: a reader, or anyone when <c>AllowSelfView</c> is on.</summary>
    public const string SelfOrRead = "UserActivity.SelfOrRead";
}

public sealed class UserActivityReadRequirement : IAuthorizationRequirement;

public sealed class UserActivitySelfOrReadRequirement : IAuthorizationRequirement;

/// <summary>Resource check: may the caller see activity involving this actor (or its <c>Via</c>)?</summary>
public sealed class UserActivitySubjectRequirement : IAuthorizationRequirement
{
    public static UserActivitySubjectRequirement Instance { get; } = new();
}
