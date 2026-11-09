using System.Collections.Concurrent;

namespace Umbraco.Community.UserActivity.Export;

/// <summary>One export at a time per user (design 7.3); a second gets 429. Per server: a load-balanced site allows one per server.</summary>
public sealed class ExportGate
{
    private readonly ConcurrentDictionary<Guid, byte> _running = new();

    public bool TryEnter(Guid userKey) => _running.TryAdd(userKey, 0);

    public void Exit(Guid userKey) => _running.TryRemove(userKey, out _);
}
