using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Identity;

/// <summary>
/// Turns stored identities into users (design 3.5). Batched (one call per identity kind per page) and cached in the
/// request cache, so a page of 100 rows by 3 people costs at most a few lookups. Special values follow S5:
/// id <c>-1</c> / the super user key is the default admin, id <c>0</c> and "system" are System, anything not found
/// is Unknown and never borrows another user's name.
/// </summary>
public sealed class UserIdentityResolver
{
    private const string CacheKey = "Umbraco.Community.UserActivity.Identity";

    /// <summary>
    /// The default admin's int id. Core marks <c>DefaultAdminId</c> obsolete in favour of the key,
    /// but <c>umbracoLog</c> stores ints, so rows logged against the default admin still say -1 (S5).
    /// </summary>
    private const int DefaultAdminId = -1;

    private readonly IUserService _userService;
    private readonly IRequestCache _requestCache;

    public UserIdentityResolver(IUserService userService, IRequestCache requestCache)
    {
        _userService = userService;
        _requestCache = requestCache;
    }

    /// <summary>The user filter, resolved once per request; null when no user has this key.</summary>
    public async Task<ResolvedUser?> ResolveFilterAsync(Guid key, CancellationToken cancellationToken)
    {
        IUser? user = await _userService.GetAsync(key);
        return user is null ? null : new ResolvedUser(user.Key, user.Id, user.Username, user.Email, null);
    }

    public async Task<IReadOnlyDictionary<ActorRef, ActorDisplay>> ResolveManyAsync(IEnumerable<ActorRef> refs, CancellationToken cancellationToken)
    {
        Dictionary<ActorRef, ActorDisplay> cache = Cache();
        var wanted = refs.Distinct().ToList();
        var missing = wanted.Where(r => !cache.ContainsKey(r)).ToList();

        if (missing.Count > 0)
        {
            foreach ((ActorRef r, ActorDisplay d) in await LookupAsync(missing, cancellationToken))
            {
                cache[r] = d;
            }
        }

        return wanted.ToDictionary(r => r, r => cache[r]);
    }

    private async Task<Dictionary<ActorRef, ActorDisplay>> LookupAsync(List<ActorRef> refs, CancellationToken cancellationToken)
    {
        var result = new Dictionary<ActorRef, ActorDisplay>();

        var ids = refs.Where(r => r.Kind == ActorRefKind.Id && r.Id != 0).Select(r => r.Id).Distinct().ToArray();
        Dictionary<int, IUser> byId = ids.Length == 0 ? [] : _userService.GetUsersById(ids).ToDictionary(u => u.Id);

        var keys = refs.Where(r => r.Kind == ActorRefKind.Key).Select(r => r.Key).Distinct().ToArray();
        Dictionary<Guid, IUser> byKey = keys.Length == 0 ? [] : (await _userService.GetAsync(keys)).ToDictionary(u => u.Key);

        foreach (ActorRef r in refs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[r] = r.Kind switch
            {
                ActorRefKind.Id when r.Id == 0 => System(),
                ActorRefKind.Id => byId.TryGetValue(r.Id, out IUser? u) ? Found(u) : NotFound(r),
                ActorRefKind.Key => byKey.TryGetValue(r.Key, out IUser? u) ? Found(u) : NotFound(r),
                ActorRefKind.Username or ActorRefKind.Text when r.Text == "system" => System(),
                // Usernames have no batch lookup in core; only uSync uses them, one call per distinct name per page.
                ActorRefKind.Username => _userService.GetByUsername(r.Text!) is { } u ? Found(u) : NotFound(r),
                _ => NotFound(r),
            };
        }

        return result;
    }

    private static ActorDisplay Found(IUser user)
    {
        ActorKind kind = user.Id == DefaultAdminId || user.Key == Constants.Security.SuperUserKey
            ? ActorKind.DefaultAdmin
            : ActorKind.User;
        return new ActorDisplay(kind, user.Key, user.Id, user.Name, user.Name ?? kind.ToString());
    }

    private static ActorDisplay NotFound(ActorRef r)
    {
        if ((r.Kind == ActorRefKind.Id && r.Id == DefaultAdminId)
            || (r.Kind == ActorRefKind.Key && r.Key == Constants.Security.SuperUserKey))
        {
            return new ActorDisplay(ActorKind.DefaultAdmin, Constants.Security.SuperUserKey, DefaultAdminId, null, "Default admin");
        }

        Guid? key = r.Kind == ActorRefKind.Key ? r.Key : null;
        int? id = r.Kind == ActorRefKind.Id ? r.Id : null;
        return new ActorDisplay(ActorKind.Unknown, key, id, null, ActorDisplay.UnknownLabel(key, id));
    }

    private static ActorDisplay System() => new(ActorKind.System, null, 0, null, "System");

    private Dictionary<ActorRef, ActorDisplay> Cache()
    {
        if (!_requestCache.IsAvailable)
        {
            return [];
        }

        if (_requestCache.Get(CacheKey) is Dictionary<ActorRef, ActorDisplay> existing)
        {
            return existing;
        }

        var created = new Dictionary<ActorRef, ActorDisplay>();
        _requestCache.Set(CacheKey, created);
        return created;
    }
}
