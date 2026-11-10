using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Tests.Configuration;

namespace Umbraco.Community.UserActivity.Tests.Sources;

public class SourceSettingsTests
{
    internal static SourceAvailability Availability(UserActivityOptions options, params IUserActivitySource[] sources)
        => new(new UserActivitySourceCollection(() => sources), new StaticOptionsMonitor<UserActivityOptions>(options));

    [Fact]
    public void Sources_DefaultEnabled()
    {
        SourceAvailability availability = Availability(OptionsTests.Bind(), new FakeSource("core"), new FakeSource("ai"));

        Assert.True(availability.IsEnabled("core"));
        Assert.Equal(["core", "ai"], availability.GetActive().Select(s => s.Alias));
    }

    [Theory]
    [InlineData("UserActivity:Sources:ai:Enabled")]
    [InlineData("UserActivity:Sources:AI:Enabled")]
    public void Sources_NameCaseInsensitive(string key)
    {
        UserActivityOptions options = OptionsTests.Bind(new Dictionary<string, string?> { [key] = "false" });
        SourceAvailability availability = Availability(options, new FakeSource("ai"), new FakeSource("usync"));

        Assert.False(availability.IsEnabled("ai"));
        Assert.False(availability.IsEnabled("AI"));
        Assert.Equal(["usync"], availability.GetActive().Select(s => s.Alias));
    }

    [Fact]
    public void Sources_ConfigNamesMatchAliases()
    {
        // The documented config names (design 5) map onto the permanent aliases.
        UserActivityOptions options = OptionsTests.Bind(new Dictionary<string, string?>
        {
            ["UserActivity:Sources:uSync:Enabled"] = "false",
            ["UserActivity:Sources:ArchivedUsers:Enabled"] = "false",
        });
        SourceAvailability availability = Availability(options);

        Assert.False(availability.IsEnabled(SourceAlias.USync));
        Assert.False(availability.IsEnabled(SourceAlias.ArchivedUsers));
        Assert.True(availability.IsEnabled(SourceAlias.Core));
    }

    [Fact]
    public void Sources_Disabled_LeftOutOfEngineAndFilters()
    {
        UserActivityOptions options = OptionsTests.Bind(new Dictionary<string, string?> { ["UserActivity:Sources:Core:Enabled"] = "false" });
        SourceAvailability availability = Availability(options, new FakeSource("core"), new FakeSource("automate"));

        Assert.Equal(["automate"], availability.GetActive().Select(s => s.Alias));
        Assert.Null(availability.GetActive("core"));
        Assert.NotNull(availability.GetActive("automate"));
    }

    [Fact]
    public void Unavailable_HiddenEvenIfEnabled()
    {
        UserActivityOptions options = OptionsTests.Bind(new Dictionary<string, string?> { ["UserActivity:Sources:AI:Enabled"] = "true" });
        var ai = new FakeSource("ai") { IsAvailable = false };
        SourceAvailability availability = Availability(options, new FakeSource("core"), ai);

        Assert.Equal(["core"], availability.GetActive().Select(s => s.Alias));
        Assert.Null(availability.GetActive("ai"));
    }

    [Fact]
    public void Sources_UnknownName_OneWarningNoError()
    {
        UserActivityOptions options = OptionsTests.Bind(new Dictionary<string, string?>
        {
            ["UserActivity:Sources:Nonsense:Enabled"] = "false",
            // Built-in names never warn, installed or not: config is shared between sites with different packages.
            ["UserActivity:Sources:AI:Enabled"] = "true",
            ["UserActivity:Sources:FormsAuditTrail:Enabled"] = "true",
        });
        var logger = new FakeLogger<SourceAvailability>();

        Availability(options, new FakeSource("core")).LogUnknownSettings(logger);

        FakeLogRecord record = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains("Nonsense", record.Message);
    }

    [Fact]
    public void Sources_ThirdPartyAlias_NoWarning()
    {
        UserActivityOptions options = OptionsTests.Bind(new Dictionary<string, string?> { ["UserActivity:Sources:MySource:Enabled"] = "false" });
        var logger = new FakeLogger<SourceAvailability>();
        SourceAvailability availability = Availability(options, new FakeSource("mySource"));

        availability.LogUnknownSettings(logger);

        Assert.Empty(logger.Collector.GetSnapshot());
        Assert.Empty(availability.GetActive());
    }

    [Fact]
    public void ThrowingIsAvailable_IsUnavailable_OthersStay()
    {
        // M3 review QA-1: one source's IsAvailable must not take every Activity request down.
        var broken = NSubstitute.Substitute.For<IUserActivitySource>();
        NSubstitute.SubstituteExtensions.Returns(broken.Alias, "broken");
        NSubstitute.SubstituteExtensions.Returns(broken.IsAvailable, _ => throw new MissingMethodException("x", "y"));
        var fine = new FakeSource("fine");
        var availability = new SourceAvailability(new UserActivitySourceCollection(() => [broken, fine]),
            new StaticOptionsMonitor<Umbraco.Community.UserActivity.Configuration.UserActivityOptions>(new()));

        Assert.Equal(["fine"], availability.GetActive().Select(s => s.Alias));
    }

    [Fact]
    public void NoLicenceChecks_InPackageSource()
    {
        // Pascal P5: no licence checks anywhere. Only README text may mention licences.
        string src = Path.Combine(RepoPaths.Root, "src", "Umbraco.Community.UserActivity");
        string[] hits = Directory.EnumerateFiles(src, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.Ordinal) || f.EndsWith(".ts", StringComparison.Ordinal))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains("Licen", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(src, f))
            .ToArray();

        Assert.Empty(hits);
    }
}

/// <summary>An options monitor over a fixed value, for unit tests.</summary>
internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
