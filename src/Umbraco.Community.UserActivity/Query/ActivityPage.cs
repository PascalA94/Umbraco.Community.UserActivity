using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Query;

/// <summary>One page of merged events, plus everything the API needs to describe it.</summary>
/// <param name="Actors">Every actor and <c>Via</c> on the page, resolved in one batch.</param>
/// <param name="UserNotFound">The user filter named no user.</param>
public sealed record ActivityPage(
    IReadOnlyList<ActivityEvent> Items,
    int? Total,
    bool TotalIsExact,
    bool DepthCapped,
    IReadOnlyList<SourceResult> Sources,
    IReadOnlyDictionary<ActorRef, ActorDisplay> Actors,
    bool UserNotFound = false)
{
    public static ActivityPage NotFound { get; } = new([], 0, true, false, [], new Dictionary<ActorRef, ActorDisplay>(), UserNotFound: true);

    public override string ToString() => $"ActivityPage {{ Items = {Items.Count}, Total = {Total} }}";
}

/// <summary>One event with its resolved actors, for the detail view.</summary>
public sealed record ActivityDetail(ActivityEvent Event, IReadOnlyDictionary<ActorRef, ActorDisplay> Actors)
{
    public override string ToString() => $"ActivityDetail {{ {Event} }}";
}
