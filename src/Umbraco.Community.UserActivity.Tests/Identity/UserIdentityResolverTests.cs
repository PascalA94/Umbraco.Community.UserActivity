using NSubstitute;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Tests.Identity;

public class UserIdentityResolverTests
{
    private readonly IUserService _users = Substitute.For<IUserService>();
    private readonly List<IUser> _known = [];
    private readonly DictionaryAppCache _requestCache = new();

    public UserIdentityResolverTests()
    {
        _users.GetUsersById(Arg.Any<int[]>()).Returns(c => _known.Where(u => c.Arg<int[]>().Contains(u.Id)).ToList());
        _users.GetAsync(Arg.Any<IEnumerable<Guid>>()).Returns(c =>
        {
            var keys = c.Arg<IEnumerable<Guid>>().ToHashSet();
            return Task.FromResult<IEnumerable<IUser>>(_known.Where(u => keys.Contains(u.Key)).ToList());
        });
        _users.GetAsync(Arg.Any<Guid>()).Returns(c => Task.FromResult(_known.FirstOrDefault(u => u.Key == c.Arg<Guid>())));
        _users.GetByUsername(Arg.Any<string>()).Returns(c =>
            _known.FirstOrDefault(u => string.Equals(u.Username, c.Arg<string>(), StringComparison.OrdinalIgnoreCase)));
    }

    private UserIdentityResolver Resolver() => new(_users, _requestCache);

    private IUser AddUser(int id, string name, Guid? key = null)
    {
        IUser user = Substitute.For<IUser>();
        user.Id.Returns(id);
        user.Key.Returns(key ?? Guid.NewGuid());
        user.Name.Returns(name);
        user.Username.Returns($"{name.Replace(' ', '.').ToLowerInvariant()}@example.test");
        user.Email.Returns($"{name.Replace(' ', '.').ToLowerInvariant()}@example.test");
        _known.Add(user);
        return user;
    }

    [Theory]
    [InlineData("id", "-1", ActorKind.DefaultAdmin)]
    [InlineData("id", "0", ActorKind.System)]
    [InlineData("id", "42", ActorKind.Unknown)]
    [InlineData("key", "6f7c8a1e-0000-4000-8000-000000000001", ActorKind.Unknown)]
    [InlineData("superUserKey", "", ActorKind.DefaultAdmin)]
    [InlineData("keyString", "system", ActorKind.System)]
    [InlineData("username", "system", ActorKind.System)]
    public async Task Resolver_SpecialIds(string kind, string value, ActorKind expected)
    {
        AddUser(-1, "Administrator", Constants.Security.SuperUserKey);
        ActorRef actorRef = kind switch
        {
            "id" => ActorRef.ForId(int.Parse(value)),
            "key" => ActorRef.ForKey(Guid.Parse(value)),
            "superUserKey" => ActorRef.ForKey(Constants.Security.SuperUserKey),
            "keyString" => ActorRef.ForKeyString(value),
            _ => ActorRef.ForUsername(value),
        };

        IReadOnlyDictionary<ActorRef, ActorDisplay> result = await Resolver().ResolveManyAsync([actorRef], CancellationToken.None);

        ActorDisplay display = result[actorRef];
        Assert.Equal(expected, display.Kind);
        if (expected == ActorKind.Unknown)
        {
            Assert.Null(display.Name);
            Assert.Contains(kind == "id" ? "id 42" : "key 6f7c8a1e", display.Label);
        }

        if (expected == ActorKind.DefaultAdmin)
        {
            Assert.Equal("Administrator", display.Name);
        }
    }

    [Fact]
    public async Task Resolver_KnownUser_ByIdKeyAndKeyString()
    {
        IUser wren = AddUser(7, "Wren Writer");
        ActorRef[] refs = [ActorRef.ForId(7), ActorRef.ForKey(wren.Key), ActorRef.ForKeyString(wren.Key.ToString("D"))];

        IReadOnlyDictionary<ActorRef, ActorDisplay> result = await Resolver().ResolveManyAsync(refs, CancellationToken.None);

        Assert.All(refs, r =>
        {
            Assert.Equal(ActorKind.User, result[r].Kind);
            Assert.Equal("Wren Writer", result[r].Name);
            Assert.Equal(wren.Key, result[r].UserKey);
            Assert.Equal(7, result[r].UserId);
        });
    }

    [Fact]
    public async Task Resolver_UsernameCaseInsensitive()
    {
        IUser wren = AddUser(7, "Wren Writer");
        ActorRef upper = ActorRef.ForUsername("WREN.WRITER@EXAMPLE.TEST");

        IReadOnlyDictionary<ActorRef, ActorDisplay> result = await Resolver().ResolveManyAsync([upper], CancellationToken.None);

        Assert.Equal(ActorRef.ForUsername("wren.writer@example.test"), upper);
        Assert.Equal(wren.Key, result[upper].UserKey);
        Assert.Equal("Wren Writer", result[upper].Name);
    }

    [Fact]
    public async Task Resolver_BatchesOneLookupPerKind()
    {
        IUser a = AddUser(7, "A User");
        IUser b = AddUser(8, "B User");
        ActorRef[] refs =
        [
            ActorRef.ForId(7), ActorRef.ForId(8), ActorRef.ForId(7), ActorRef.ForId(99),
            ActorRef.ForKey(a.Key), ActorRef.ForKey(b.Key), ActorRef.ForKeyString(b.Key.ToString()),
        ];

        await Resolver().ResolveManyAsync(refs, CancellationToken.None);

        _users.Received(1).GetUsersById(Arg.Any<int[]>());
        await _users.Received(1).GetAsync(Arg.Any<IEnumerable<Guid>>());
        await _users.DidNotReceive().GetAsync(Arg.Any<Guid>());
    }

    [Fact]
    public async Task Resolver_CachesPerRequest()
    {
        AddUser(7, "A User");

        await Resolver().ResolveManyAsync([ActorRef.ForId(7)], CancellationToken.None);
        IReadOnlyDictionary<ActorRef, ActorDisplay> second = await Resolver().ResolveManyAsync([ActorRef.ForId(7)], CancellationToken.None);

        _users.Received(1).GetUsersById(Arg.Any<int[]>());
        Assert.Equal("A User", second[ActorRef.ForId(7)].Name);
    }

    [Fact]
    public async Task Resolver_DeletedUser_UsesStoredName()
    {
        var actor = new ActivityActor(ActorKind.User, null, 42, "Old Name", null);
        IReadOnlyDictionary<ActorRef, ActorDisplay> result = await Resolver().ResolveManyAsync([ActorRef.For(actor)!.Value], CancellationToken.None);

        ActorDisplay display = ActorDisplay.Compose(actor, result);

        Assert.Equal(ActorKind.Unknown, display.Kind);
        Assert.Equal("Old Name", display.Name);
        Assert.Contains("id 42", display.Label);
    }

    // QA-F7: uSync stores only the username, so its actors carry neither a key nor an id.
    [Fact]
    public void ActorRef_UserActorWithOnlyAStoredName_IsLookedUpByUsername()
    {
        var actor = new ActivityActor(ActorKind.User, null, null, "Wren.Writer@Example.test", null);

        Assert.Equal(ActorRef.ForUsername("wren.writer@example.test"), ActorRef.For(actor));
    }

    [Fact]
    public void ActorRef_SystemOrAutomationActorWithAStoredName_IsNotLookedUp()
    {
        Assert.Null(ActorRef.For(new ActivityActor(ActorKind.System, null, null, "uSync (no user)", null)));
        Assert.Null(ActorRef.For(new ActivityActor(ActorKind.Automation, null, null, "Nightly import", null)));
    }

    [Fact]
    public async Task Compose_UsernameOnlyActor_ResolvesToTheUserWithAKey()
    {
        IUser wren = AddUser(7, "Wren Writer");
        var actor = new ActivityActor(ActorKind.User, null, null, wren.Username, null);
        IReadOnlyDictionary<ActorRef, ActorDisplay> result = await Resolver().ResolveManyAsync([ActorRef.For(actor)!.Value], CancellationToken.None);

        ActorDisplay display = ActorDisplay.Compose(actor, result);

        Assert.Equal(ActorKind.User, display.Kind);
        Assert.Equal(wren.Key, display.UserKey);
        Assert.Equal("Wren Writer", display.Name);
    }

    [Fact]
    public async Task Resolver_RenamedUser_ShowsCurrentName()
    {
        IUser wren = AddUser(7, "Wren Renamed");
        var actor = new ActivityActor(ActorKind.User, wren.Key, null, "Wren Old", null);
        IReadOnlyDictionary<ActorRef, ActorDisplay> result = await Resolver().ResolveManyAsync([ActorRef.For(actor)!.Value], CancellationToken.None);

        Assert.Equal("Wren Renamed", ActorDisplay.Compose(actor, result).Name);
    }

    [Fact]
    public async Task Compose_AutomationKind_KeepsSourceKind()
    {
        // A source that knows better (Automate's scheduled run) keeps its kind; only User-kind actors are reclassified.
        var actor = new ActivityActor(ActorKind.Automation, null, null, null, null);

        ActorDisplay display = ActorDisplay.Compose(actor, new Dictionary<ActorRef, ActorDisplay>());

        Assert.Equal(ActorKind.Automation, display.Kind);
        Assert.Null(ActorRef.For(actor));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task ResolveFilter_KnownKey_ReturnsIdUsernameEmail()
    {
        IUser wren = AddUser(7, "Wren Writer");

        ResolvedUser? user = await Resolver().ResolveFilterAsync(wren.Key, CancellationToken.None);

        Assert.NotNull(user);
        Assert.Equal(7, user.Id);
        Assert.Equal(wren.Key, user.Key);
        Assert.Equal("wren.writer@example.test", user.Username);
        Assert.Equal("wren.writer@example.test", user.Email);
    }

    [Fact]
    public async Task ResolveFilter_UnknownKey_Null()
        => Assert.Null(await Resolver().ResolveFilterAsync(Guid.NewGuid(), CancellationToken.None));
}
