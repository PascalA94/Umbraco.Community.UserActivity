using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.UserActivity.Recorder.Handlers;

internal static class RecorderHandlerRegistration
{
    /// <summary>
    /// Registers <typeparamref name="THandler"/> and routes <typeparamref name="TNotification"/> to one of its methods.
    /// Only ever called from a guard's non-inlined registration method, after the package's probe passed.
    /// </summary>
    public static IServiceCollection AddRecorderHandler<TNotification, THandler>(this IServiceCollection services, Func<THandler, Action<TNotification>> method)
        where TNotification : INotification
        where THandler : class
    {
        services.AddTransient<THandler>();
        return services.AddTransient<INotificationHandler<TNotification>>(sp =>
            new RecorderNotificationHandler<TNotification>(method(sp.GetRequiredService<THandler>())));
    }
}
