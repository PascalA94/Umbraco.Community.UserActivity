using System.Collections.Concurrent;

namespace Umbraco.Community.UserActivity.Query;

/// <summary>
/// Sources that hit a binding failure (<see cref="TypeLoadException"/>, <see cref="MissingMemberException"/>) stay
/// failed until restart (design 3.2): the integration's shape changed under a probe that passed, and retrying would
/// throw again on every request. A singleton, because the engine is transient.
/// </summary>
public sealed class SourceFailures
{
    private readonly ConcurrentDictionary<string, byte> _broken = new(StringComparer.Ordinal);

    public bool IsBroken(string alias) => _broken.ContainsKey(alias);

    /// <summary>True the first time, so the caller logs once.</summary>
    internal bool MarkBroken(string alias) => _broken.TryAdd(alias, 0);

    internal static bool IsBindingFailure(Exception ex) => ex is TypeLoadException or MissingMemberException;
}
