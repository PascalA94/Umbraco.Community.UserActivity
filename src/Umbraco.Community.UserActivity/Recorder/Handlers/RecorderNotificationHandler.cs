using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Community.UserActivity.Recorder.Handlers;

/// <summary>
/// Adapts a delegate to <see cref="INotificationHandler{TNotification}"/>. The per-package handler classes deliberately
/// do not implement that interface: a class implementing <c>INotificationHandler&lt;CspSavedNotification&gt;</c> cannot
/// load without CSP Manager, and Umbraco's type scan loads every type of every package assembly at boot (design 3.2).
/// This open generic type loads anywhere; it is only closed over an optional notification inside a guard's
/// non-inlined registration method.
/// </summary>
internal sealed class RecorderNotificationHandler<TNotification> : INotificationHandler<TNotification>
    where TNotification : INotification
{
    private readonly Action<TNotification> _handle;

    public RecorderNotificationHandler(Action<TNotification> handle) => _handle = handle;

    public void Handle(TNotification notification) => _handle(notification);
}
