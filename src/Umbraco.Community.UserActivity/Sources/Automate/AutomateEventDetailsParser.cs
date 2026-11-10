using System.Text.Json;

namespace Umbraco.Community.UserActivity.Sources.Automate;

/// <summary>How far an Automate <c>eventDetails</c> value could be read.</summary>
internal enum AutomateDetailsStatus
{
    /// <summary>An object with an <c>initiatedBy</c> of the form <c>&lt;kind&gt;:&lt;id&gt;</c> or a bare kind.</summary>
    Ok,

    /// <summary>Not JSON, not an object, or no usable <c>initiatedBy</c>: show the performer and flag it.</summary>
    UnknownShape,

    /// <summary>Nested deeper than <see cref="AutomateEventDetailsParser.MaxDepth"/>: the row is skipped.</summary>
    Skip,
}

/// <summary>The parts of Automate's audit details we use (S3). Every value is untrusted stored text.</summary>
internal sealed record AutomateDetails(
    AutomateDetailsStatus Status,
    string? AutomationId,
    string? RunId,
    string? StepId,
    string? Workspace,
    string? InitiatedBy,
    string? InitiatorKind,
    Guid? InitiatorUserKey);

/// <summary>
/// Reads Automate's <c>umbracoAudit.eventDetails</c>, e.g.
/// <c>{"automationId":..,"runId":..,"stepId":..,"workspace":..,"initiatedBy":"user:&lt;key&gt;"}</c> (S3, both lines).
/// Only <c>user:&lt;guid&gt;</c> names a person; every other kind (<c>replay</c>, <c>scheduled</c>, <c>webhook</c>,
/// <c>system</c>, <c>ai-agent</c>, unknown) is automation, and the id after <c>replay:</c> is a run, never a user.
/// </summary>
internal static class AutomateEventDetailsParser
{
    /// <summary>Automate's shape is flat; a deeper value is not Automate's and is skipped, never walked.</summary>
    public const int MaxDepth = 16;

    private static readonly AutomateDetails Unknown = new(AutomateDetailsStatus.UnknownShape, null, null, null, null, null, null, null);
    private static readonly AutomateDetails Skipped = new(AutomateDetailsStatus.Skip, null, null, null, null, null, null, null);

    public static AutomateDetails Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Unknown;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = MaxDepth });
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Unknown;
            }

            string? initiatedBy = String(root, "initiatedBy");
            (string? kind, Guid? userKey) = Initiator(initiatedBy);
            if (kind is null)
            {
                return Unknown with { AutomationId = String(root, "automationId"), RunId = String(root, "runId") };
            }

            return new AutomateDetails(
                AutomateDetailsStatus.Ok,
                String(root, "automationId"),
                String(root, "runId"),
                String(root, "stepId"),
                String(root, "workspace"),
                initiatedBy,
                kind,
                userKey);
        }
        catch (JsonException)
        {
            return TooDeep(json) ? Skipped : Unknown;
        }
    }

    /// <summary>Valid JSON that only failed on depth (checked by reading tokens, not by the exception's wording).</summary>
    private static bool TooDeep(string json)
    {
        try
        {
            var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(json), new JsonReaderOptions { MaxDepth = 1024 });
            var deepest = 0;
            while (reader.Read())
            {
                deepest = Math.Max(deepest, reader.CurrentDepth + 1);
            }

            return deepest > MaxDepth;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>The kind before the first colon, and the user key for <c>user:&lt;guid&gt;</c>; null kind if unusable.</summary>
    private static (string? Kind, Guid? UserKey) Initiator(string? initiatedBy)
    {
        if (string.IsNullOrEmpty(initiatedBy))
        {
            return (null, null);
        }

        var colon = initiatedBy.IndexOf(':', StringComparison.Ordinal);
        var kind = colon < 0 ? initiatedBy : initiatedBy[..colon];
        if (!string.Equals(kind, "user", StringComparison.Ordinal))
        {
            return (kind, null);
        }

        // "user:" without a key can't be attributed to anyone: fall back to the performer.
        return colon >= 0 && Guid.TryParseExact(initiatedBy[(colon + 1)..], "D", out Guid key) ? ("user", key) : (null, null);
    }

    private static string? String(JsonElement root, string name)
        => root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
