using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Identity;

/// <summary>
/// How an actor is shown: the classified kind, the current user data when found, and a fallback label for unknown
/// users ("Unknown user (id 42)"). The client localises by kind; the label is the English fallback.
/// </summary>
public sealed record ActorDisplay(ActorKind Kind, Guid? UserKey, int? UserId, string? Name, string Label)
{
    public override string ToString() => $"ActorDisplay {{ Kind = {Kind} }}";

    /// <summary>
    /// Combines what the source stored with what the resolver found. Only identities are reclassified (User,
    /// ServiceAccount, DefaultAdmin, Unknown); a source's Automation or System kind stands. The current name wins,
    /// and the stored name is the fallback for deleted users (design 3.5).
    /// </summary>
    public static ActorDisplay Compose(ActivityActor actor, IReadOnlyDictionary<ActorRef, ActorDisplay> resolved)
    {
        if (actor.Kind is ActorKind.Automation or ActorKind.System)
        {
            return new ActorDisplay(actor.Kind, actor.UserKey, actor.UserId, actor.StoredName, actor.StoredName ?? actor.Kind.ToString());
        }

        if (ActorRef.For(actor) is not { } actorRef || !resolved.TryGetValue(actorRef, out ActorDisplay? found))
        {
            return new ActorDisplay(ActorKind.Unknown, actor.UserKey, actor.UserId, actor.StoredName, UnknownLabel(actor.UserKey, actor.UserId));
        }

        ActorKind kind = actor.Kind == ActorKind.ServiceAccount && found.Kind == ActorKind.User ? ActorKind.ServiceAccount : found.Kind;
        string? name = found.Name ?? actor.StoredName;
        return found with { Kind = kind, Name = name, Label = found.Name ?? found.Label };
    }

    internal static string UnknownLabel(Guid? key, int? id)
        => key is { } k ? $"Unknown user (key {k.ToString("D")[..8]}...)" : id is { } i ? $"Unknown user (id {i})" : "Unknown user";
}
