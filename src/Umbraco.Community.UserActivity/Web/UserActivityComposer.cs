using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.UserActivity.Web;

/// <summary>The package's only composer. Register services, handlers and options here.</summary>
public sealed class UserActivityComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // Nothing to register yet. Keep this class the single entry point so the integration tests and the demo site
        // exercise the same wiring a real site gets.
    }
}
