using System.Reflection;

namespace Umbraco.Community.UserActivity.Sources.Adapters;

/// <summary>A type, method or property an adapter relies on, named by strings so the guard never binds to it.</summary>
/// <param name="ParameterTypeNames">Method parameters by full type name, or null to accept any overload by name.</param>
/// <param name="ValueTypeName">A property's full type name, or null to accept any.</param>
public sealed record MemberSpec(MemberSpecKind Kind, string TypeName, string? MemberName, IReadOnlyList<string>? ParameterTypeNames, string? ValueTypeName)
{
    public static MemberSpec Type(string typeName) => new(MemberSpecKind.Type, typeName, null, null, null);

    public static MemberSpec Method(string typeName, string methodName, params string[] parameterTypeNames)
        => new(MemberSpecKind.Method, typeName, methodName, parameterTypeNames.Length == 0 ? null : parameterTypeNames, null);

    public static MemberSpec Property(string typeName, string propertyName, string? valueTypeName = null)
        => new(MemberSpecKind.Property, typeName, propertyName, null, valueTypeName);

    public string DisplayName => MemberName is null ? TypeName : $"{TypeName}.{MemberName}";
}

public enum MemberSpecKind
{
    Type,
    Method,
    Property,
}

/// <param name="MissingMember">The first member not found, by name only (no values, safe to log).</param>
public sealed record ProbeResult(bool Ok, string? MissingMember)
{
    public static ProbeResult Success { get; } = new(true, null);
}

/// <summary>
/// Checks, by reflection, that an installed optional assembly still has every member an adapter calls with the
/// signature it was compiled against (design 3.2). A failed probe hides the source instead of crashing later.
/// </summary>
public static class AdapterProbe
{
    private const BindingFlags Members = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;

    public static ProbeResult Check(Assembly assembly, IReadOnlyList<MemberSpec> specs)
    {
        foreach (MemberSpec spec in specs)
        {
            bool found;
            try
            {
                found = Exists(assembly, spec);
            }
            catch (Exception ex) when (ex is TypeLoadException or FileNotFoundException or FileLoadException or BadImageFormatException or AmbiguousMatchException)
            {
                found = false;
            }

            if (!found)
            {
                return new ProbeResult(false, spec.DisplayName);
            }
        }

        return ProbeResult.Success;
    }

    private static bool Exists(Assembly assembly, MemberSpec spec)
    {
        Type? type = assembly.GetType(spec.TypeName, throwOnError: false);
        if (type is null)
        {
            return false;
        }

        return spec.Kind switch
        {
            MemberSpecKind.Type => true,
            MemberSpecKind.Method => AllMethods(type).Any(m => m.Name == spec.MemberName && ParametersMatch(m, spec.ParameterTypeNames)),
            MemberSpecKind.Property => AllProperties(type).Any(p => p.Name == spec.MemberName
                && (spec.ValueTypeName is null || p.PropertyType.FullName == spec.ValueTypeName)),
            _ => false,
        };
    }

    private static bool ParametersMatch(MethodInfo method, IReadOnlyList<string>? expected)
        => expected is null || method.GetParameters().Select(p => p.ParameterType.FullName).SequenceEqual(expected);

    // Interfaces don't expose inherited interface members through GetMethods, so walk them too.
    private static IEnumerable<MethodInfo> AllMethods(Type type)
        => type.GetMethods(Members).Concat(type.IsInterface ? type.GetInterfaces().SelectMany(i => i.GetMethods(Members)) : []);

    private static IEnumerable<PropertyInfo> AllProperties(Type type)
        => type.GetProperties(Members).Concat(type.IsInterface ? type.GetInterfaces().SelectMany(i => i.GetProperties(Members)) : []);
}
