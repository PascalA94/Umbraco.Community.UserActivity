using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.UserActivity.Tests;

public class ScaffoldTests
{
#if UMBRACO_17
    private const int ExpectedMajor = 17;
    private const string ExpectedRange = "[17.8.0, 18.0.0)";
#else
    private const int ExpectedMajor = 18;
    private const string ExpectedRange = "[18.3.0, 19.0.0)";
#endif

    [Fact]
    public void Assembly_TargetsSelectedMajor()
    {
        // The CMS assembly the package compiled against must be the line UmbracoMajor selected, or the switch is broken.
        Version? cms = typeof(IUmbracoBuilder).Assembly.GetName().Version;
        Assert.Equal(ExpectedMajor, cms?.Major);
    }

    [Fact]
    public void Nuspec_DependsOnUmbracoOnly()
    {
        // One package, no add-ons (design 3.2): optional integrations are compile-only, so the nuspec may only name
        // Umbraco packages. Packs the already-built Release output for this line into a temp folder.
        var outDir = Directory.CreateTempSubdirectory("ua-pack-").FullName;
        try
        {
            RunDotnet($"pack \"{RepoPaths.PackageProject}\" -c Release --no-build -p:UmbracoMajor={ExpectedMajor} -o \"{outDir}\" -v q");
            var nupkg = Directory.GetFiles(outDir, "*.nupkg").Single(f => !f.EndsWith(".snupkg", StringComparison.Ordinal));
            using ZipArchive zip = ZipFile.OpenRead(nupkg);
            using Stream nuspecStream = zip.Entries.Single(e => e.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
            XDocument nuspec = XDocument.Load(nuspecStream);
            XNamespace ns = nuspec.Root!.Name.Namespace;

            var groups = nuspec.Descendants(ns + "dependencies").Elements(ns + "group").ToList();
            Assert.NotEmpty(groups);
            Assert.Equal(groups.Count, groups.Select(g => (string?)g.Attribute("targetFramework")).Distinct().Count());
            foreach (XElement group in groups)
            {
                var deps = group.Elements(ns + "dependency").ToList();
                Assert.NotEmpty(deps);
                Assert.All(deps, d => Assert.StartsWith("Umbraco.Cms.", (string)d.Attribute("id")!, StringComparison.Ordinal));
                // The portfolio floors (17.8.0 / 18.3.0) sit above the 27 Oct 2026 advisories; a broken UmbracoRange
                // must fail here, not ship.
                Assert.All(deps, d => Assert.Equal(ExpectedRange, (string)d.Attribute("version")!));
            }

            // Only our own assembly ships in lib/.
            var libDlls = zip.Entries.Where(e => e.FullName.StartsWith("lib/", StringComparison.Ordinal) && e.FullName.EndsWith(".dll", StringComparison.Ordinal))
                .Select(e => e.Name).ToList();
            Assert.Equal(["Umbraco.Community.UserActivity.dll"], libDlls);

            // Nuspec_HasNoAIDependency (plan Task 22): no optional integration's assembly anywhere in the package.
            string[] optional = ["Umbraco.AI", "uSync", "Umbraco.Community.ArchivedUsers", "Umbraco.Community.FormsAuditTrail"];
            Assert.DoesNotContain(zip.Entries, e => optional.Any(o => e.Name.StartsWith(o, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Directory.Delete(outDir, recursive: true);
        }
    }

    private static void RunDotnet(string args)
    {
        using var process = Process.Start(new ProcessStartInfo("dotnet", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = RepoPaths.Root,
        })!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"dotnet {args} failed: {stdout.Result}{stderr.Result}");
    }
}
