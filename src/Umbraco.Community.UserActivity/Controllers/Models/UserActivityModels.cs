namespace Umbraco.Community.UserActivity.Controllers.Models;

// Response shapes are versioned by the v1 route (design 5): fields may be added, never renamed or removed.
// Details and RawType values are not versioned.

public sealed class ActivityPageResponseModel
{
    /// <summary>Exact when <see cref="TotalIsExact"/>; otherwise a lower bound ("1,000+").</summary>
    public int? Total { get; init; }

    public bool TotalIsExact { get; init; }

    public bool DepthCapped { get; init; }

    public required ActivityEventResponseModel[] Items { get; init; }

    public required SourceStatusResponseModel[] Sources { get; init; }
}

public class ActivityEventResponseModel
{
    public required string Source { get; init; }

    public required string Id { get; init; }

    public DateTimeOffset OccurredUtc { get; init; }

    public required ActivityActorResponseModel Actor { get; init; }

    public required string Category { get; init; }

    public required string RawType { get; init; }

    public ActivityEntityResponseModel? Entity { get; init; }

    public string? Summary { get; init; }

    /// <summary>Null unless <c>ShowIpAddresses</c> is on and the caller is a reader.</summary>
    public string? IpAddress { get; init; }
}

public sealed class ActivityEventDetailResponseModel : ActivityEventResponseModel
{
    /// <summary>For humans; may change in a minor.</summary>
    public IReadOnlyDictionary<string, string>? Details { get; init; }
}

public sealed class ActivityActorResponseModel
{
    /// <summary><c>user</c>, <c>serviceAccount</c>, <c>automation</c>, <c>system</c>, <c>defaultAdmin</c> or <c>unknown</c>.</summary>
    public required string Kind { get; init; }

    public Guid? UserKey { get; init; }

    /// <summary>The current name, or the stored name for a deleted user.</summary>
    public string? Name { get; init; }

    public string? StoredName { get; init; }

    /// <summary>English fallback, e.g. "Unknown user (id 42)"; the client localises by kind.</summary>
    public required string Label { get; init; }

    public ActivityActorResponseModel? Via { get; init; }
}

public sealed class ActivityEntityResponseModel
{
    public required string Type { get; init; }

    public string? Key { get; init; }

    public string? Id { get; init; }

    public string? Name { get; init; }

    public bool Exists { get; init; }
}

public sealed class SourceStatusResponseModel
{
    public required string Alias { get; init; }

    /// <summary><c>ok</c>, <c>failed</c> or <c>timedOut</c>.</summary>
    public required string Status { get; init; }

    public int? Count { get; init; }
}

public sealed class ActivityFilterOptionsResponseModel
{
    public required ActivitySourceOptionResponseModel[] Sources { get; init; }

    public required string[] Categories { get; init; }

    public required string[] EntityTypes { get; init; }

    public int DefaultRangeDays { get; init; }

    public int MaxRangeDays { get; init; }

    public int MaxTake { get; init; }

    /// <summary>The caller may read everyone's activity (the global view).</summary>
    public bool CanReadAll { get; init; }

    public bool ExportEnabled { get; init; }

    public bool ShowIpAddresses { get; init; }
}

public sealed class ActivitySourceOptionResponseModel
{
    public required string Alias { get; init; }

    public required string DisplayNameKey { get; init; }

    public bool CanCount { get; init; }

    public bool CanSearch { get; init; }

    /// <summary>Days the underlying store keeps rows, when known; null when its owner keeps them.</summary>
    public int? RetentionHintDays { get; init; }

    /// <summary>True when the source keeps rows until its owner deletes them and no limit is set (the recorder with <c>RetentionDays</c> 0).</summary>
    public bool RetentionKeptForever { get; init; }
}
