using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.UserActivity.DemoSite.Seed;

/// <summary>
/// Demo only, never packed: creates synthetic users and content so the Activity views have something to show.
/// Runs in Development only and is idempotent.
/// </summary>
public sealed class DemoSeedComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, DemoSeeder>();
}
