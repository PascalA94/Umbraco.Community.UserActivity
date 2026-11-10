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
        // The one exception is the recorder's own table, communityUserActivityEvent (M5), see WriteHits.
        Assert.Empty(WriteHits(ProductionFiles()));
    }

    [Fact]
    public void ArchitectureTests_WriteRule_AllowsOnlyTheRecorderTable_InTheRecorderFolder()
    {
        // The rule has to keep catching everything else, so prove it on synthetic files.
        const string recorderFile = "Recorder/RecorderSql.cs";
        const string writerFile = "Recorder/RecorderWriter.cs";
        const string ownTable = "class N { string Table = s.GetQuotedTableName(RecorderTable.Name); } var q = $\"DELETE FROM {n.Table}\";";
        const string ownInsert = "ActivityEventDto dto = ToDto(e); scope.Database.Insert(dto);";

        Assert.Empty(WriteHits([(recorderFile, ownTable), (writerFile, ownInsert)]));

        // The same code outside the recorder folder, or a write to anything else, is a hit.
        Assert.NotEmpty(WriteHits([("Sources/Core/CoreSql.cs", ownTable)]));
        Assert.NotEmpty(WriteHits([("Sources/Core/CoreSql.cs", ownInsert)]));

        // Bypass shapes: a statement that merely mentions the recorder table later, and the recorder's DTO insert
        // anywhere but the writer. The delete is allowed in RecorderSql.cs only, the insert in RecorderWriter.cs only.
        const string quoted = "class N { string Table = s.GetQuotedTableName(RecorderTable.Name); } ";
        Assert.NotEmpty(WriteHits([(recorderFile, quoted + "var q = $\"INSERT INTO umbracoLog (a) SELECT a FROM {n.Table}\";")]));
        Assert.NotEmpty(WriteHits([(recorderFile, quoted + "var q = $\"DELETE FROM umbracoLog WHERE id IN (SELECT id FROM {n.Table})\";")]));
        Assert.NotEmpty(WriteHits([("Recorder/RecorderSource.cs", ownInsert)]));
        Assert.NotEmpty(WriteHits([(writerFile, "UserDto dto = Load(); scope.Database.Insert(dto);")]));
        Assert.NotEmpty(WriteHits([(writerFile, ownInsert + " scope.Database.Insert(other);")]));
        Assert.NotEmpty(WriteHits([("Recorder/RecorderSource.cs", ownTable)]));
        Assert.NotEmpty(WriteHits([(recorderFile, "var q = \"DELETE FROM umbracoLog WHERE 1 = 1\";")]));
        Assert.NotEmpty(WriteHits([(recorderFile, "var q = $\"UPDATE {x} SET a = 1\";")]));
        Assert.NotEmpty(WriteHits([(recorderFile, "scope.Database.Insert(someOtherRow);")]));
        Assert.NotEmpty(WriteHits([(recorderFile, "scope.Database.Delete<UserDto>(1);")]));
        Assert.NotEmpty(WriteHits([(recorderFile, "var q = \"DROP TABLE communityUserActivityEvent\";")]));
    }

    private static List<string> WriteHits(IEnumerable<(string Path, string Code)> files)
        => files
            .SelectMany(f => WriteSql().Matches(f.Code).Select(m => (f.Path, f.Code, Match: m)))
            .Where(h => !IsRecorderTableWrite(h.Path, h.Code, h.Match))
            .Select(h => $"{h.Path}: {h.Match.Value}")
            .ToList();

    /// <summary>
    /// The recorder's two writes, each pinned to its one file: <c>.Insert(dto)</c> of the <c>ActivityEventDto</c> in
    /// Recorder/RecorderWriter.cs, and a <c>DELETE FROM</c> whose target comes directly after FROM (the recorder table
    /// token or its literal name) in Recorder/RecorderSql.cs. Matching the target right after FROM, not anywhere in the
    /// statement, stops <c>DELETE FROM other WHERE x IN (SELECT ... {n.Table})</c> riding on the allowance. Raw
    /// <c>INSERT INTO</c> and DDL (DROP, ALTER, TRUNCATE) are never allowed: the migration plan creates the table through
    /// core's builder and rows go in through the DTO.
    /// </summary>
    private static bool IsRecorderTableWrite(string path, string code, Match match)
    {
        var segments = Segments(path);
        var after = code[(match.Index + match.Length)..];

        if (match.Value.StartsWith('.'))
        {
            return segments.SequenceEqual(["Recorder", "RecorderWriter.cs"])
                && Regex.IsMatch(match.Value, @"^\.Insert\s*\($")
                && Regex.IsMatch(after, @"^\s*dto\s*\)")
                && code.Contains("ActivityEventDto dto", StringComparison.Ordinal);
        }

        if (!Regex.IsMatch(match.Value, @"^DELETE\s+FROM$", RegexOptions.IgnoreCase) || !segments.SequenceEqual(["Recorder", "RecorderSql.cs"]))
        {
            return false;
        }

        return Regex.IsMatch(after, @"^\s+(\{n\.Table\}|\[?communityUserActivityEvent\]?)(\s|""|$)")
            && (!after.TrimStart().StartsWith("{n.Table}", StringComparison.Ordinal) || code.Contains("GetQuotedTableName(RecorderTable.Name)", StringComparison.Ordinal));
    }

    [Fact]
    public void ArchitectureTests_EveryTypeLoads_WithoutOptionalAssemblies()
    {
        // Umbraco's type scan calls GetTypes() on every package assembly at boot and fails start-up if one type can't
        // load (design 3.2). This process has none of the optional integrations, so any adapter type whose layout
        // needs one (a struct field, an async state machine awaiting one of their tuples) fails here first.
        Assembly assembly = typeof(IUserActivitySource).Assembly;

        Exception? failure = Record.Exception(() => assembly.GetTypes());

        Assert.Null(failure is ReflectionTypeLoadException rtle
            ? string.Join("; ", rtle.LoaderExceptions.Select(e => e?.Message).Distinct())
            : failure?.Message);
    }

    [Fact]
    public void ArchitectureTests_AdapterPlumbingAndBuiltInSourcesAreInternal()
    {
        // M3 review QA-10: only the source contract is public API (stable within a major, README "What is versioned").
        // Guards, probes and the built-in sources can change in a minor, so they must not be public.
        string[] internalNamespaces =
        [
            "Umbraco.Community.UserActivity.Sources.Adapters", "Umbraco.Community.UserActivity.Sources.Automate",
            "Umbraco.Community.UserActivity.Sources.Forms", "Umbraco.Community.UserActivity.Sources.ArchivedUsers",
            "Umbraco.Community.UserActivity.Recorder.Handlers",
        ];
        var exposed = typeof(IUserActivitySource).Assembly.GetExportedTypes()
            .Where(t => internalNamespaces.Any(ns => t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(exposed);
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
