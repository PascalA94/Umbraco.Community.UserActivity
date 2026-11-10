using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;

namespace Umbraco.Community.UserActivity.Sources.Core;

/// <summary>
/// Bounds core's database work (M1 review QA-2). Core's reads are synchronous, so the engine's timeout can't preempt
/// them; the command timeout makes the database stop at <c>SourceTimeoutSeconds</c> instead, and the engine reports
/// the source as timed out.
/// </summary>
internal static class CoreTimeouts
{
    public static void Apply(IScope scope, UserActivityOptions options) => scope.Database.CommandTimeout = options.SourceTimeoutSeconds;

    /// <summary>
    /// Count only with indexed filters (design 3.7): a search is a LIKE over text columns, so its total would cost a
    /// second full scan. Searches report an inexact total ("N+") instead.
    /// </summary>
    public static bool ShouldCount(ActivitySourceQuery query) => query.Search is null;
}
