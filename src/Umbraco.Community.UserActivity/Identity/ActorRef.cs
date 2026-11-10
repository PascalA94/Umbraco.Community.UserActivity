using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Identity;

public enum ActorRefKind
{
    Id,
    Key,
    Username,
    Text,
}

/// <summary>
/// A raw user identity as a source stored it (design 3.5): an int id, a Guid key, a key as a string, or a username.
/// Usernames and unparseable strings compare ignoring case. <see cref="ToString"/> never prints the text.
/// </summary>
public readonly record struct ActorRef(ActorRefKind Kind, int Id, Guid Key, string? Text)
{
    public static ActorRef ForId(int id) => new(ActorRefKind.Id, id, Guid.Empty, null);

    public static ActorRef ForKey(Guid key) => new(ActorRefKind.Key, 0, key, null);

    /// <summary>Umbraco.AI stores the key as a string; a parseable one is the same identity as the Guid key.</summary>
    public static ActorRef ForKeyString(string value)
        => Guid.TryParse(value, out Guid key) ? ForKey(key) : new(ActorRefKind.Text, 0, Guid.Empty, Normalise(value));

    public static ActorRef ForUsername(string username) => new(ActorRefKind.Username, 0, Guid.Empty, Normalise(username));

    /// <summary>The identity to look up for an actor: key first, then id; null when the actor carries neither.</summary>
    public static ActorRef? For(ActivityActor actor)
        => actor.UserKey is { } key ? ForKey(key) : actor.UserId is { } id ? ForId(id) : null;

    public override string ToString() => Kind switch
    {
        ActorRefKind.Id => $"ActorRef {{ Id = {Id} }}",
        ActorRefKind.Key => $"ActorRef {{ Key = {Key} }}",
        _ => $"ActorRef {{ Kind = {Kind} }}",
    };

    private static string Normalise(string value) => value.Trim().ToLowerInvariant();
}
