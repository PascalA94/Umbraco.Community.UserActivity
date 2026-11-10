using NSubstitute;
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

    [Fact]
    public void CoreClaims_NeverAskNonClaimantsIfTheyAreAvailable()
    {
        // M3 review QA-1: IsAvailable can call into an optional integration; core's claims must only touch claimants.
        var noClaims = Substitute.For<IUserActivitySource>();
        noClaims.Alias.Returns("noclaims");
        noClaims.IsAvailable.Returns(_ => throw new MissingMethodException("x", "y"));
        var claimant = new FakeSource("claimant", auditPrefixes: ["vendor/x/"]);
        var collection = new UserActivitySourceCollection(() => [noClaims, claimant]);

        IReadOnlyList<string> prefixes = Umbraco.Community.UserActivity.Sources.Core.CoreClaims.From(() => collection).AuditPrefixes;

        Assert.Equal(["vendor/x/"], prefixes);
    }

    [Theory]
    [InlineData("User")]
    [InlineData("member")]
    [InlineData("UserGroup")]
    public void WholeTypeClaim_OnCoreOwnedLogRows_Fails(string entityType)
    {
        // M3 review QA-4: a whole-type claim would hide core's own user, member and group rows; partial claims are fine.
        var greedy = new FakeSource("greedy", logEntityTypes: [entityType]);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => SourceRegistryValidator.Validate([greedy]));

        Assert.Contains("'greedy'", ex.Message);
    }

    [Fact]
    public async Task CoreClaims_AsRegistered_ResolveWithoutACycle()
    {
        // The core source needs CoreClaims and CoreClaims reads the collection that holds the core source, so the
        // registration must read it lazily. Resolving it eagerly deadlocked the backoffice host at boot (M3 fix pass).
        var builder = new UserActivitySourceCollectionBuilder();
        builder.Append([typeof(Umbraco.Community.UserActivity.Sources.Core.CoreSource), typeof(FakeClaimant)]);
        var services = new ServiceCollection();
        builder.RegisterWith(services);
        services.AddSingleton(NSubstitute.Substitute.For<Umbraco.Cms.Infrastructure.Scoping.IScopeProvider>());
        services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<Umbraco.Community.UserActivity.Configuration.UserActivityOptions>>(
            new StaticOptionsMonitor<Umbraco.Community.UserActivity.Configuration.UserActivityOptions>(new()));
        Umbraco.Community.UserActivity.Web.UserActivityComposer.AddCoreClaims(services);
        ServiceProvider provider = services.BuildServiceProvider();

        var resolve = Task.Run(() => provider.GetRequiredService<Umbraco.Community.UserActivity.Sources.Core.CoreClaims>().AuditPrefixes);

        Task finished = await Task.WhenAny(resolve, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.True(finished == resolve, "resolving CoreClaims and the collection did not finish");
        Assert.Equal(["vendor/claimant/"], await resolve);
    }

    public sealed class FakeClaimant() : FakeSource("claimant", auditPrefixes: ["vendor/claimant/"]);
}
