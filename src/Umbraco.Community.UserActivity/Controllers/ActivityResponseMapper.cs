using Umbraco.Community.UserActivity.Controllers.Models;
using Umbraco.Community.UserActivity.Identity;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Controllers;

/// <summary>Engine results to API models. IPs only reach readers (Pascal, P8).</summary>
internal static class ActivityResponseMapper
{
    public static ActivityPageResponseModel ToModel(ActivityPage page, bool isReader) => new()
    {
        Total = page.Total,
        TotalIsExact = page.TotalIsExact,
        DepthCapped = page.DepthCapped,
        Items = page.Items.Select(e => ToItem(e, page.Actors, isReader)).ToArray(),
        Sources = page.Sources.Select(s => new SourceStatusResponseModel { Alias = s.Alias, Status = s.Status, Count = s.Count }).ToArray(),
    };

    public static ActivityEventDetailResponseModel ToDetail(ActivityDetail detail, bool isReader) => new()
    {
        Source = detail.Event.Source,
        Id = detail.Event.Id,
        OccurredUtc = detail.Event.OccurredUtc,
        Actor = Actor(detail.Event.Actor, detail.Actors),
        Category = detail.Event.Category,
        RawType = detail.Event.RawType,
        Entity = Entity(detail.Event.Entity),
        Summary = detail.Event.Summary,
        IpAddress = isReader ? detail.Event.IpAddress : null,
        // Readers only (M1 review SEC-1): core's details hold other people's names and emails, e.g. the default
        // admin as performer on a user's own sign-in rows (S5).
        Details = isReader ? detail.Event.Details : null,
    };

    private static ActivityEventResponseModel ToItem(ActivityEvent e, IReadOnlyDictionary<ActorRef, ActorDisplay> actors, bool isReader)
        => new()
        {
            Source = e.Source,
            Id = e.Id,
            OccurredUtc = e.OccurredUtc,
            Actor = Actor(e.Actor, actors),
            Category = e.Category,
            RawType = e.RawType,
            Entity = Entity(e.Entity),
            Summary = e.Summary,
            IpAddress = isReader ? e.IpAddress : null,
        };

    private static ActivityActorResponseModel Actor(ActivityActor actor, IReadOnlyDictionary<ActorRef, ActorDisplay> actors)
    {
        ActorDisplay display = ActorDisplay.Compose(actor, actors);
        return new ActivityActorResponseModel
        {
            Kind = KindName(display.Kind),
            UserKey = display.UserKey,
            Name = display.Name,
            StoredName = actor.StoredName,
            Label = display.Label,
            Via = actor.Via is null ? null : Actor(actor.Via, actors),
        };
    }

    private static ActivityEntityResponseModel? Entity(ActivityEntity? entity)
        => entity is null ? null : new ActivityEntityResponseModel { Type = entity.Type, Key = entity.Key, Id = entity.Id, Name = entity.Name, Exists = entity.Exists };

    public static string KindName(ActorKind kind) => kind switch
    {
        ActorKind.User => "user",
        ActorKind.ServiceAccount => "serviceAccount",
        ActorKind.Automation => "automation",
        ActorKind.System => "system",
        ActorKind.DefaultAdmin => "defaultAdmin",
        _ => "unknown",
    };
}
