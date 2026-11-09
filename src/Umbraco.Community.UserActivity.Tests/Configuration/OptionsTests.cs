using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Community.UserActivity.Configuration;

namespace Umbraco.Community.UserActivity.Tests.Configuration;

public class OptionsTests
{
    internal static UserActivityOptions Bind(IDictionary<string, string?>? values = null)
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(values ?? new Dictionary<string, string?>()).Build();
        var options = new UserActivityOptions();
        config.GetSection(UserActivityOptions.SectionName).Bind(options);
        return options;
    }

    private static ValidateOptionsResult Validate(UserActivityOptions options)
        => new UserActivityOptionsValidator().Validate(Options.DefaultName, options);

    [Fact]
    public void Options_Defaults_MatchDesign()
    {
        UserActivityOptions o = Bind();

        Assert.Equal(["admin"], o.EffectiveAllowedUserGroups);
        Assert.False(o.AllowSelfView);
        Assert.Equal(30, o.DefaultRangeDays);
        Assert.Equal(366, o.MaxRangeDays);
        Assert.Equal(5000, o.MaxDepth);
        Assert.Equal(5, o.SourceTimeoutSeconds);
        Assert.False(o.ShowIpAddresses);
        Assert.True(o.LogViews);
        Assert.False(o.Export.Enabled);
        Assert.Equal(10000, o.Export.MaxRows);
        Assert.Equal(0, o.Recorder.RetentionDays);
        Assert.Empty(o.Sources);
        Assert.True(Validate(o).Succeeded);
    }

    [Fact]
    public void Options_NegativeRetention_FailsNamingKey()
    {
        UserActivityOptions o = Bind(new Dictionary<string, string?> { ["UserActivity:Recorder:RetentionDays"] = "-7" });

        ValidateOptionsResult result = Validate(o);

        Assert.True(result.Failed);
        Assert.Contains("UserActivity:Recorder:RetentionDays", result.FailureMessage);
        Assert.DoesNotContain("-7", result.FailureMessage);
    }

    [Fact]
    public void Options_RangeOver366_Fails()
    {
        UserActivityOptions o = Bind(new Dictionary<string, string?> { ["UserActivity:MaxRangeDays"] = "400" });

        ValidateOptionsResult result = Validate(o);

        Assert.True(result.Failed);
        Assert.Contains("UserActivity:MaxRangeDays", result.FailureMessage);
        Assert.DoesNotContain("400", result.FailureMessage);
    }

    [Fact]
    public void Options_DefaultRangeOverMax_Fails()
    {
        UserActivityOptions o = Bind(new Dictionary<string, string?>
        {
            ["UserActivity:MaxRangeDays"] = "60",
            ["UserActivity:DefaultRangeDays"] = "90",
        });

        ValidateOptionsResult result = Validate(o);

        Assert.True(result.Failed);
        Assert.Contains("UserActivity:DefaultRangeDays", result.FailureMessage);
    }

    [Theory]
    [InlineData("UserActivity:MaxDepth", "0")]
    [InlineData("UserActivity:SourceTimeoutSeconds", "0")]
    [InlineData("UserActivity:Export:MaxRows", "0")]
    [InlineData("UserActivity:DefaultRangeDays", "0")]
    public void Options_NonPositiveLimits_FailNamingKey(string key, string value)
    {
        ValidateOptionsResult result = Validate(Bind(new Dictionary<string, string?> { [key] = value }));

        Assert.True(result.Failed);
        Assert.Contains(key, result.FailureMessage);
    }

    [Fact]
    public void Options_ConfiguredGroups_ReplaceTheDefault()
    {
        // QA-1: the binder appends to an initialised array, so a default of ["admin"] could never be removed.
        UserActivityOptions o = Bind(new Dictionary<string, string?> { ["UserActivity:AllowedUserGroups:0"] = "auditors" });

        Assert.Equal(["auditors"], o.EffectiveAllowedUserGroups);
    }

    [Fact]
    public void Options_ExportMaxRowsOverCeiling_Fails()
    {
        ValidateOptionsResult result = Validate(Bind(new Dictionary<string, string?> { ["UserActivity:Export:MaxRows"] = "2147483647" }));

        Assert.True(result.Failed);
        Assert.Contains("UserActivity:Export:MaxRows", result.FailureMessage);
    }

    [Fact]
    public void Options_NoAllowedGroups_Fails()
    {
        UserActivityOptions o = Bind();
        o.AllowedUserGroups = [" "];

        ValidateOptionsResult result = Validate(o);

        Assert.True(result.Failed);
        Assert.Contains("UserActivity:AllowedUserGroups", result.FailureMessage);
    }
}
