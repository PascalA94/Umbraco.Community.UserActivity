using Umbraco.Community.CSPManager.Models;
using Umbraco.Community.CSPManager.Notifications;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Recorder.Handlers.CspManager;

/// <summary>
/// CSP Manager's save (design 4.1). The notification carries only the saved definition, and the user comes from the
/// backoffice accessor (S4). CSP Manager raises nothing on delete, so only saves are recorded.
/// Allow-listed details: the on/off flags and how many sources the policy has, never the sources themselves or any
/// other policy text, because a policy is configuration an attacker would like to read.
/// </summary>
internal sealed class CspHandlers
{
    private readonly RecorderHandlerContext _context;

    public CspHandlers(RecorderHandlerContext context) => _context = context;

    public void OnSaved(CspSavedNotification notification)
        => _context.Record(RecorderSources.Csp, () => [ToEntry(nameof(CspSavedNotification), notification.CspDefinition)]);

    private static RecorderEntry ToEntry(string rawType, CspDefinition definition)
        => new(
            RecorderSources.Csp,
            Categories.Saved,
            rawType,
            null,
            null,
            "cspPolicy",
            definition.Id.ToString(),
            definition.IsBackOffice ? "Back office policy" : "Front end policy",
            new Dictionary<string, string>
            {
                ["enabled"] = RecorderDetails.Flag(definition.Enabled),
                ["reportOnly"] = RecorderDetails.Flag(definition.ReportOnly),
                ["backOffice"] = RecorderDetails.Flag(definition.IsBackOffice),
                ["sourceCount"] = (definition.Sources?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
}
