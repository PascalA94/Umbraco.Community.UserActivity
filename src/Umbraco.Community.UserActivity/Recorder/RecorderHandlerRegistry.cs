namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>
/// Which recorded packages have a handler registered. The composer creates one instance and each handler guard
/// (M5 Tasks 32-33) calls <see cref="Register"/> only after its package passed the guard, so the recorder source
/// can tell "no recorded package is installed" from "installed, nothing recorded yet".
/// An instance, not a static: a mutable static would break the no-statics rule (pitfall P2).
/// </summary>
internal sealed class RecorderHandlerRegistry
{
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);

    public bool Any => _names.Count > 0;

    public IReadOnlyCollection<string> Names => _names;

    /// <summary>Called from the composer, one thread, before the container is built.</summary>
    public void Register(string name) => _names.Add(name);
}
