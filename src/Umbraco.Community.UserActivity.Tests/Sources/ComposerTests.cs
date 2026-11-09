using Microsoft.Extensions.DependencyInjection;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Tests.Sources;

public class ComposerTests
{
    private static UserActivitySourceCollection Build(params Type[] types)
    {
        var builder = new UserActivitySourceCollectionBuilder();
        builder.Append(types);
        var services = new ServiceCollection();
        builder.RegisterWith(services);
        return services.BuildServiceProvider().GetRequiredService<UserActivitySourceCollection>();
    }

    [Fact]
    public void Composer_DuplicateAlias_FailsNamingBoth()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Build(typeof(DupOne), typeof(DupTwo)));

        Assert.Contains("'dup'", ex.Message);
        Assert.Contains(nameof(DupOne), ex.Message);
        Assert.Contains(nameof(DupTwo), ex.Message);
    }

    [Fact]
    public void Composer_DuplicateClaim_FailsNamingBoth()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Build(typeof(ClaimsAutomate), typeof(ClaimsAutomateToo)));

        Assert.Contains("umbraco/automate/", ex.Message);
        Assert.Contains("'claimone'", ex.Message);
        Assert.Contains("'claimtwo'", ex.Message);
    }

    [Fact]
    public void Composer_OverlappingClaimPrefix_Fails()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Build(typeof(ClaimsAutomate), typeof(ClaimsUmbraco)));

        Assert.Contains("'claimone'", ex.Message);
        Assert.Contains("'claimbroad'", ex.Message);
    }

    [Fact]
    public void Composer_DuplicateLogEntityClaim_FailsNamingBoth()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Build(typeof(ClaimsForm), typeof(ClaimsFormToo)));

        Assert.Contains("'form'", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("'formone'", ex.Message);
        Assert.Contains("'formtwo'", ex.Message);
    }

    [Theory]
    [InlineData(typeof(ClaimsEmpty))]
    [InlineData(typeof(ClaimsCoreUser))]
    [InlineData(typeof(ClaimsOwnRows))]
    [InlineData(typeof(ClaimsBroadUmbraco))]
    public void Composer_ClaimOnCoreOwnedOrEmptyPrefix_Fails(Type source)
        => Assert.Throws<InvalidOperationException>(() => Build(source));

    [Fact]
    public void Composer_ClaimOverlapIgnoringCase_Fails()
        => Assert.Throws<InvalidOperationException>(() => Build(typeof(ClaimsAutomate), typeof(ClaimsAutomateUpper)));

    [Fact]
    public void Composer_InvalidAlias_Fails()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => Build(typeof(BadAlias)));

        Assert.Contains(nameof(BadAlias), ex.Message);
    }

    [Fact]
    public void Composer_DistinctSources_Build()
    {
        UserActivitySourceCollection sources = Build(typeof(DupOne), typeof(ClaimsAutomate), typeof(ClaimsForm));

        Assert.Equal(["dup", "claimone", "formone"], sources.Select(s => s.Alias));
    }

    public sealed class DupOne() : FakeSource("dup");

    public sealed class DupTwo() : FakeSource("dup");

    public sealed class BadAlias() : FakeSource("Bad-Alias");

    public sealed class ClaimsAutomate() : FakeSource("claimone", auditPrefixes: ["umbraco/automate/"]);

    public sealed class ClaimsAutomateToo() : FakeSource("claimtwo", auditPrefixes: ["umbraco/automate/"]);

    public sealed class ClaimsUmbraco() : FakeSource("claimbroad", auditPrefixes: ["umbraco/automate/run/"]);

    public sealed class ClaimsForm() : FakeSource("formone", logEntityTypes: ["Form"]);

    public sealed class ClaimsEmpty() : FakeSource("claimempty", auditPrefixes: [" "]);

    public sealed class ClaimsCoreUser() : FakeSource("claimcore", auditPrefixes: ["umbraco/user/sign-in"]);

    public sealed class ClaimsOwnRows() : FakeSource("claimown", auditPrefixes: ["community/useractivity/"]);

    public sealed class ClaimsBroadUmbraco() : FakeSource("claimbroad", auditPrefixes: ["umbraco/"]);

    public sealed class ClaimsAutomateUpper() : FakeSource("claimupper", auditPrefixes: ["Umbraco/Automate/"]);

    public sealed class ClaimsFormToo() : FakeSource("formtwo", logEntityTypes: ["form"]);
}
