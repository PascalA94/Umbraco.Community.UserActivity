using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Extensions;

namespace Umbraco.Community.UserActivity.Security;

/// <summary>
/// The one place the access rules live (design 7.2, Pascal P4):
/// <list type="bullet">
/// <item>Readers (a group in <c>AllowedUserGroups</c>, compared ignoring case, <b>and</b> Users section access) see
/// everything, including their own tab.</item>
/// <item>With <c>AllowSelfView</c> on, anyone else sees only events where they are the actor or the <c>Via</c>, and
/// never the global view.</item>
/// </list>
/// Registered as a singleton, never scoped: a scoped handler crashes start-up under scope validation (S6).
/// </summary>
public sealed class UserActivityAuthorizationHandler : IAuthorizationHandler
{
    private readonly IUserService _userService;
    private readonly IOptionsMonitor<UserActivityOptions> _options;

    public UserActivityAuthorizationHandler(IUserService userService, IOptionsMonitor<UserActivityOptions> options)
    {
        _userService = userService;
        _options = options;
    }

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pending = context.PendingRequirements.ToList();
        if (!pending.Any(r => r is UserActivityReadRequirement or UserActivitySelfOrReadRequirement or UserActivitySubjectRequirement))
        {
            return;
        }

        Guid? key = context.User.Identity?.GetUserKey();
        IUser? caller = key is { } k ? await _userService.GetAsync(k) : null;
        if (caller is null || !caller.IsApproved || caller.IsLockedOut)
        {
            return;
        }

        UserActivityOptions options = _options.CurrentValue;
        var isReader = IsReader(caller, options);

        foreach (IAuthorizationRequirement requirement in pending)
        {
            var allowed = requirement switch
            {
                UserActivityReadRequirement => isReader,
                UserActivitySelfOrReadRequirement => isReader || options.AllowSelfView,
                UserActivitySubjectRequirement => isReader
                    || (options.AllowSelfView && context.Resource is ActivityActor actor && ActivityQueryEngine.Involves(actor, Self(caller))),
                _ => false,
            };

            if (allowed)
            {
                context.Succeed(requirement);
            }
        }
    }

    /// <summary>A reader: in an allowed group (alias, ignoring case) and with Users section access (server alias <c>users</c>, S6).</summary>
    public static bool IsReader(IUser user, UserActivityOptions options)
    {
        var allowed = new HashSet<string>(options.EffectiveAllowedUserGroups.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()), StringComparer.OrdinalIgnoreCase);
        return user.Groups.Any(g => allowed.Contains(g.Alias))
            && user.AllowedSections.Contains(Constants.Applications.Users, StringComparer.OrdinalIgnoreCase);
    }

    private static ResolvedUser Self(IUser user) => new(user.Key, user.Id, user.Username, user.Email, null);
}
