using System.Text.Json;

namespace Umbraco.Community.UserActivity.Tests;

public class PackageManifestTests
{
    [Theory]
    [InlineData("Client/public/umbraco-package.json")]
    [InlineData("wwwroot/umbraco-package.json")]
    public void Manifest_IdentityIsPermanent(string relativePath)
    {
        // These names are permanent once released (plan Global Constraints): renaming breaks installed sites.
        var path = Path.Combine(RepoPaths.Root, "src", "Umbraco.Community.UserActivity", relativePath);
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = doc.RootElement;

        Assert.Equal("Umbraco.Community.UserActivity", root.GetProperty("name").GetString());
        JsonElement bundle = Assert.Single(root.GetProperty("extensions").EnumerateArray());
        Assert.Equal("bundle", bundle.GetProperty("type").GetString());
        Assert.Equal("UserActivity.Bundle", bundle.GetProperty("alias").GetString());
        Assert.Equal("/App_Plugins/UserActivity/index.js", bundle.GetProperty("js").GetString());
    }

    [Fact]
    public void PackageConstants_ArePermanent()
    {
        Assert.Equal("Umbraco.Community.UserActivity", PackageConstants.PackageName);
        Assert.Equal("UserActivity", PackageConstants.AppPluginsFolder);
        Assert.Equal("UserActivity", PackageConstants.AliasPrefix);
    }
}
