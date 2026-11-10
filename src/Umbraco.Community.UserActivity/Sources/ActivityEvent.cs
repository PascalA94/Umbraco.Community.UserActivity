namespace Umbraco.Community.UserActivity.Sources;

/// <summary>
/// One normalised event (design 3.4). Every string is untrusted stored data and is rendered as text only.
/// <see cref="ToString"/> prints the source and id only, so an event can't leak names into logs.
/// </summary>
/// <param name="Id">Opaque and unique within <paramref name="Source"/>, e.g. <c>log:123456</c>.</param>
/// <param name="Category">One of <see cref="Categories"/>.</param>
/// <param name="RawType">As stored by the owning package; not versioned.</param>
/// <param name="Summary">Plain text, at most 500 characters.</param>
/// <param name="IpAddress">Null unless <c>ShowIpAddresses</c> is on.</param>
/// <param name="Details">Detail view only: at most 50 entries of 2,000 characters; not versioned.</param>
public sealed record ActivityEvent(
    string Source,
    string Id,
    DateTimeOffset OccurredUtc,
    ActivityActor Actor,
    string Category,
    string RawType,
    ActivityEntity? Entity,
    string? Summary,
    string? IpAddress,
    IReadOnlyDictionary<string, string>? Details)
{
    public const int MaxSummaryLength = 500;
    public const int MaxDetails = 50;
    public const int MaxDetailLength = 2000;

    public override string ToString() => $"ActivityEvent {{ Source = {Source}, Id = {Id} }}";
}

/// <summary>Who did it. <paramref name="Via"/> is the account that carried it out for them (an Automate service account).</summary>
/// <param name="StoredName">The name the source kept at the time, the fallback for deleted or renamed users.</param>
public sealed record ActivityActor(ActorKind Kind, Guid? UserKey, int? UserId, string? StoredName, ActivityActor? Via)
{
    public override string ToString() => $"ActivityActor {{ Kind = {Kind} }}";
}

public enum ActorKind
{
    User,
    ServiceAccount,
    Automation,
    System,
    DefaultAdmin,
    Unknown,
}

/// <param name="Exists">False when the item has since been deleted.</param>
public sealed record ActivityEntity(string Type, string? Key, string? Id, string? Name, bool Exists)
{
    public override string ToString() => $"ActivityEntity {{ Type = {Type}, Id = {Id} }}";
}
