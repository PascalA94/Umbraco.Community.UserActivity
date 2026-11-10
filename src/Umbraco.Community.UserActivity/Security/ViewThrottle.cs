using System.Collections.Concurrent;

namespace Umbraco.Community.UserActivity.Security;

/// <summary>When each viewer last had a view of each subject logged. Singleton, in memory, per server.</summary>
public sealed class ViewThrottle
{
    private readonly ConcurrentDictionary<(Guid Viewer, Guid Subject), DateTimeOffset> _last = new();

    /// <summary>True (and recorded) when no view of this pair was logged within <paramref name="window"/>.</summary>
    public bool TryAcquire(Guid viewer, Guid subject, DateTimeOffset now, TimeSpan window)
    {
        // Drop stale pairs now and then so the map stays as small as the last hour's activity.
        if (_last.Count > 1000)
        {
            foreach (KeyValuePair<(Guid, Guid), DateTimeOffset> stale in _last.Where(p => now - p.Value >= window))
            {
                _last.TryRemove(stale);
            }
        }

        var acquired = false;
        _last.AddOrUpdate(
            (viewer, subject),
            _ => { acquired = true; return now; },
            (_, last) =>
            {
                if (now - last >= window)
                {
                    acquired = true;
                    return now;
                }

                acquired = false;
                return last;
            });
        return acquired;
    }

    /// <summary>Gives the slot back after a failed write, so the next view is recorded.</summary>
    public void Release(Guid viewer, Guid subject) => _last.TryRemove((viewer, subject), out _);
}
