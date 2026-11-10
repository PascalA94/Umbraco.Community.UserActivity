using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Security;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Tests.Sources;

namespace Umbraco.Community.UserActivity.Tests.Security;

/// <summary>QA-8: the Subject rule against Via-only actors, and refused callers, without the HTTP stack.</summary>
public class AuthorizationHandlerTests
{
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly UserActivityOptions _options = new() { AllowSelfView = true };

    private IUser Caller(bool approved = true, bool locked = false, string group = "writer")
    {
        IUser user = Substitute.For<IUser>();
        user.Key.Returns(Guid.NewGuid());
        user.Id.Returns(42);
        user.IsApproved.Returns(approved);
        user.IsLockedOut.Returns(locked);
        IReadOnlyUserGroup g = Substitute.For<IReadOnlyUserGroup>();
        g.Alias.Returns(group);
        user.Groups.Returns([g]);
        user.AllowedSections.Returns(group == "admin" ? [Constants.Applications.Users] : [Constants.Applications.Content]);
        _users.GetAsync(user.Key).Returns(user);
        return user;
    }

    private async Task<bool> Allowed(IUser caller, IAuthorizationRequirement requirement, object? resource = null)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Constants.Security.OpenIdDictSubClaimType, caller.Key.ToString())], "test"));
        var context = new AuthorizationHandlerContext([requirement], principal, resource);
        await new UserActivityAuthorizationHandler(_users, new StaticOptionsMonitor<UserActivityOptions>(_options)).HandleAsync(context);
        return context.HasSucceeded;
    }

    [Fact]
    public async Task Subject_ViaIsCaller_Allowed_NeitherIsCaller_Refused()
    {
        IUser caller = Caller();
        var viaCaller = new ActivityActor(ActorKind.User, Guid.NewGuid(), 7, null, new ActivityActor(ActorKind.ServiceAccount, caller.Key, 42, null, null));
        var someoneElse = new ActivityActor(ActorKind.User, Guid.NewGuid(), 7, null, new ActivityActor(ActorKind.ServiceAccount, Guid.NewGuid(), 8, null, null));

        Assert.True(await Allowed(caller, UserActivitySubjectRequirement.Instance, viaCaller));
        Assert.False(await Allowed(caller, UserActivitySubjectRequirement.Instance, someoneElse));
        _options.AllowSelfView = false;
        Assert.False(await Allowed(caller, UserActivitySubjectRequirement.Instance, viaCaller));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task DisabledOrLockedReader_Refused(bool approved, bool locked)
    {
        IUser admin = Caller(approved, locked, "admin");

        Assert.False(await Allowed(admin, new UserActivityReadRequirement()));
        Assert.False(await Allowed(admin, new UserActivitySelfOrReadRequirement()));
    }

    [Fact]
    public async Task ActiveReader_Allowed()
        => Assert.True(await Allowed(Caller(group: "admin"), new UserActivityReadRequirement()));
}
