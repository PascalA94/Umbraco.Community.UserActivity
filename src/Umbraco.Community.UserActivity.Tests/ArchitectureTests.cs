using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Tests;

/// <summary>Rules the design states that a unit test can hold the code to (design 3.2, 7.3; pitfalls P2).</summary>
public partial class ArchitectureTests
{
    private static readonly string PackageSource = Path.Combine(RepoPaths.Root, "src", "Umbraco.Community.UserActivity");

    /// <summary>Optional packages' root namespaces. Their types may only be named inside their adapter folder.</summary>
    private static readonly string[] OptionalNamespaces =
    [
        "Umbraco.AI", "uSync", "Umbraco.Automate", "Umbraco.Forms", "Umbraco.Community.ArchivedUsers",
        "Umbraco.Community.FormsAuditTrail", "Umbraco.Workflow", "Skybrud", "UrlTracker", "Umbraco.Community.CSPManager",
    ];

    private static IEnumerable<(string Path, string Code)> ProductionFiles()
        => Directory.EnumerateFiles(PackageSource, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Segments(f).Any(s => s is "obj" or "bin" or "node_modules" or "Client"))
            .Select(f => (Path.GetRelativePath(PackageSource, f), File.ReadAllText(f)));

    private static string[] Segments(string path) => path.Split(Path.DirectorySeparatorChar);

    [Fact]
    public void ArchitectureTests_NoWritesToOtherTables()
    {
        // Read-only on other stores: no SQL writes anywhere (view rows go through IAuditEntryService by design).
        // M5 may write communityUserActivityEvent only.
        var hits = ProductionFiles()
            .SelectMany(f => WriteSql().Matches(f.Code).Select(m => $"{f.Path}: {m.Value}"))
            .Where(h => !h.Contains("communityUserActivityEvent", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(hits);
    }

    [Fact]
    public void ArchitectureTests_NoStatics()
    {
        // P2 [Critical]: no static that can hold request state. Statics must be const, or readonly and set once.
        Assembly assembly = typeof(IUserActivitySource).Assembly;
        var offenders = assembly.GetTypes()
            .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute)) && !t.Name.Contains('<'))
            .SelectMany(t => t.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => !f.IsLiteral && !f.IsDefined(typeof(CompilerGeneratedAttribute)) && (!f.IsInitOnly || IsRequestState(f.FieldType)))
                .Select(f => $"{t.FullName}.{f.Name}")
                .Concat(t.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(p => p.SetMethod is not null || IsRequestState(p.PropertyType))
                    .Select(p => $"{t.FullName}.{p.Name}")))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ArchitectureTests_OptionalTypesOnlyInAdapterFolders()
    {
        // Nothing outside Sources/Adapters/<Name>/ (and, in M5, Recorder/Handlers/<Name>/) may name an optional
        // package's type; assembly names in string literals (the guards' checks) are fine.
        var hits = new List<string>();
        foreach ((string path, string code) in ProductionFiles())
        {
            string[] segments = Segments(path);
            bool inAdapter = (segments.Length > 3 && segments[0] == "Sources" && segments[1] == "Adapters")
                || (segments.Length > 3 && segments[0] == "Recorder" && segments[1] == "Handlers");
            if (inAdapter)
            {
                continue;
            }

            string withoutStrings = StringLiteral().Replace(Comment().Replace(code, string.Empty), "\"\"");
            hits.AddRange(OptionalNamespaces
                .Where(ns => Regex.IsMatch(withoutStrings, $@"(?<![\w.]){Regex.Escape(ns)}\."))
                .Select(ns => $"{path}: {ns}"));
        }

        Assert.Empty(hits);
    }

    private static bool IsRequestState(Type type)
        => type.Namespace?.StartsWith("Microsoft.AspNetCore.Http", StringComparison.Ordinal) == true
            || type.Name is "IScope" or "ICoreScope" or "IUser" or "IBackOfficeSecurity" or "IUmbracoContext" or "ResolvedUser" or "ActivityEvent" or "ClaimsPrincipal";

    [GeneratedRegex(@"\b(INSERT\s+INTO|UPDATE\s+\S+\s+SET|DELETE\s+FROM|MERGE\s+INTO|TRUNCATE\s+TABLE|DROP\s+TABLE|ALTER\s+TABLE)\b|\.(Insert|Update|Delete|Save|InsertBulk|BulkInsert)(Async)?\s*[<(]", RegexOptions.IgnoreCase)]
    private static partial Regex WriteSql();

    [GeneratedRegex(@"@""(?:[^""]|"""")*""|""(?:\\.|[^""\\])*""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comment();
}
