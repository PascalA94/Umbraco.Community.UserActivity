namespace Umbraco.Community.UserActivity.Sources.Adapters;

/// <summary>
/// Is an optional package installed? Answered from the names of assemblies already loaded in the app domain, so
/// it never loads an assembly, resolves one or touches a type (design 3.2). Umbraco's type scan loads installed
/// package assemblies at boot, so by the time anything asks, an installed package is loaded.
/// </summary>
public static class OptionalAssemblyGuard
{
    public static bool IsLoaded(string assemblyName)
        => AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
}
