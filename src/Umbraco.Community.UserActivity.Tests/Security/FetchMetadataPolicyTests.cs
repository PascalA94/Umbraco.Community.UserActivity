#if UMBRACO_19
using Microsoft.AspNetCore.Mvc;
using Umbraco.Community.UserActivity.Controllers;
using Umbraco.Community.UserActivity.Security;

namespace Umbraco.Community.UserActivity.Tests.Security;

public class FetchMetadataPolicyTests
{
    private static readonly Uri Host = new("https://backoffice.example.test/");

    [Fact]
    public void Absent_header_is_allowed() => Assert.True(FetchMetadataPolicy.IsAllowed([], null, null));

    [Theory]
    [InlineData("same-origin")]
    [InlineData("none")]
    public void Allowed_values_pass(string value) => Assert.True(FetchMetadataPolicy.IsAllowed([value], null, null));

    [Theory]
    [InlineData("cross-site")]
    [InlineData("same-site")]
    [InlineData("")]
    [InlineData("unknown")]
    public void Other_values_are_refused(string value) => Assert.False(FetchMetadataPolicy.IsAllowed([value], null, null));

    [Fact]
    public void Several_values_all_safe_are_allowed() => Assert.True(FetchMetadataPolicy.IsAllowed(["same-origin", "none"], null, null));

    [Fact]
    public void Several_values_with_one_unsafe_are_refused() => Assert.False(FetchMetadataPolicy.IsAllowed(["same-origin", "cross-site"], null, null));

    [Fact]
    public void Several_origins_joined_in_one_header_are_refused()
        => Assert.False(FetchMetadataPolicy.IsAllowed(["cross-site"], "https://backoffice.example.test, https://evil.test", Host));

    [Fact]
    public void Origin_equal_to_backoffice_host_is_allowed()
        => Assert.True(FetchMetadataPolicy.IsAllowed(["cross-site"], "https://backoffice.example.test", Host));

    [Fact]
    public void Origin_comparison_ignores_case_and_default_port()
        => Assert.True(FetchMetadataPolicy.IsAllowed(["cross-site"], "https://cms.example.test:443", new Uri("https://CMS.example.test/")));

    [Theory]
    [InlineData("https://evil.test")]
    [InlineData("https://backoffice.example.test:8443")]
    [InlineData("http://backoffice.example.test")]
    [InlineData("null")]
    [InlineData("not a uri")]
    [InlineData("https://backoffice.example.test.evil.test")]
    public void Other_origins_are_refused(string origin) => Assert.False(FetchMetadataPolicy.IsAllowed(["cross-site"], origin, Host));

    [Fact]
    public void Origin_is_ignored_when_no_backoffice_host_is_configured()
        => Assert.False(FetchMetadataPolicy.IsAllowed(["cross-site"], "https://backoffice.example.test", null));

    [Fact]
    public void UserActivityControllerBase_CarriesTheFilter_AndHasFiveControllers()
    {
        TypeFilterAttribute filter = Assert.Single(typeof(UserActivityControllerBase).GetCustomAttributes(typeof(TypeFilterAttribute), inherit: false).Cast<TypeFilterAttribute>());
        Assert.Equal(typeof(FetchMetadataFilter), filter.ImplementationType);

        // A sixth controller must be a conscious decision: it inherits the filter, so check it writes nothing unexpected.
        Assert.Equal(5, typeof(UserActivityControllerBase).Assembly.GetTypes().Count(t => t.IsSubclassOf(typeof(UserActivityControllerBase))));
    }
}
#endif
