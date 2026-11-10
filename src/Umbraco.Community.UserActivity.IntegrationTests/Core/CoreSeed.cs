using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Extensions;

namespace Umbraco.Community.UserActivity.IntegrationTests.Core;

// NPoco's params object[] takes nulls as SQL NULL; the nullable annotations don't say so.
#pragma warning disable CS8604

/// <summary>
/// Writes core rows with a chosen timestamp. Test-only: production code never writes these tables (the architecture
/// test scans production SQL only). Rows that don't need a fixed time go through IAuditService instead.
/// </summary>
internal static class CoreSeed
{
    public static void Log(IScopeProvider scopes, int? userId, int nodeId, string? entityType, string header, string? comment, DateTime utc, string? parameters = null)
    {
        using IScope scope = scopes.CreateScope();
        scope.Database.Execute(
            "INSERT INTO umbracoLog (userId, NodeId, entityType, Datestamp, logHeader, logComment, parameters) VALUES (@0, @1, @2, @3, @4, @5, @6)",
            userId, nodeId, entityType, utc, header, comment, parameters);
        scope.Complete();
    }

    public static void Audit(
        IScopeProvider scopes,
        int performingUserId,
        Guid? performingUserKey,
        string eventType,
        DateTime utc,
        string? eventDetails = null,
        int affectedUserId = 0,
        Guid? affectedUserKey = null,
        string? performingIp = "203.0.113.7",
        string? performingDetails = null,
        string? affectedDetails = null)
    {
        using IScope scope = scopes.CreateScope();
        scope.Database.Execute(
            "INSERT INTO umbracoAudit (performingUserId, performingUserKey, performingDetails, performingIp, eventDateUtc, affectedUserId, affectedUserKey, affectedDetails, eventType, eventDetails) VALUES (@0, @1, @2, @3, @4, @5, @6, @7, @8, @9)",
            performingUserId, performingUserKey, performingDetails, performingIp, utc, affectedUserId, affectedUserKey, affectedDetails, eventType, eventDetails);
        scope.Complete();
    }
}
