namespace Umbraco.Community.UserActivity.Sources.Adapters;

/// <summary>
/// Is an optional package installed? Answered from the names of assemblies already loaded in the app domain, so
/// it never loads an assembly, resolves one or touches a type (design 3.2). Umbraco's type scan loads installed
/// package assemblies at boot, so by the time anything asks, an installed package is loaded.
/// </summary>
internal static class OptionalAssemblyGuard
{
    public static bool IsLoaded(string assemblyName) => Find(assemblyName) is not null;

    /// <summary>The loaded assembly with this simple name, or null. Never loads one.</summary>
    public static System.Reflection.Assembly? Find(string assemblyName)
        => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
}
