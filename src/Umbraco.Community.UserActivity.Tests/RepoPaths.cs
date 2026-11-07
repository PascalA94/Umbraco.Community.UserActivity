namespace Umbraco.Community.UserActivity.Tests;

/// <summary>Finds the repository root from the test output folder, so file-based tests work locally and in CI.</summary>
internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string PackageProject => Path.Combine(Root, "src", "Umbraco.Community.UserActivity", "Umbraco.Community.UserActivity.csproj");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "eng", "lines.txt")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (eng/lines.txt) not found.");
    }
}
