using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Tests.Sources;

namespace Umbraco.Community.UserActivity.Tests.Recorder;

public class RecorderRetentionTests
{
    private static (RecorderStartupNotice Notice, FakeLogger<RecorderStartupNotice> Logger) Notice(int days, bool recorderActive = true, bool enabled = true)
    {
        var options = new UserActivityOptions { Recorder = new RecorderOptions { RetentionDays = days } };
        options.Sources[SourceAlias.Recorder] = new SourceOptions { Enabled = enabled };
        var monitor = new FixedOptions(options);
        var sources = new UserActivitySourceCollection(() => [new FakeSource(SourceAlias.Recorder) { IsAvailable = recorderActive }]);
        var logger = new FakeLogger<RecorderStartupNotice>();
        return (new RecorderStartupNotice(new SourceAvailability(sources, monitor), monitor, logger), logger);
    }

    [Fact]
    public void Retention_DefaultZero_KeepsForever_LogsSettingOnce()
    {
        (RecorderStartupNotice notice, FakeLogger<RecorderStartupNotice> logger) = Notice(days: 0);

        notice.Handle(new UmbracoApplicationStartingNotification(Umbraco.Cms.Core.RuntimeLevel.Run, false));

        FakeLogRecord line = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.Contains("UserActivity:Recorder:RetentionDays", line.Message);
    }

    [Theory]
    [InlineData(30, true, true)]
    [InlineData(0, false, true)]
    [InlineData(0, true, false)]
    public void Retention_NothingToSay_NoLine(int days, bool recorderActive, bool enabled)
    {
        // A limit is set, or the recorder is hidden or switched off, so there is nothing for the owner to decide.
        (RecorderStartupNotice notice, FakeLogger<RecorderStartupNotice> logger) = Notice(days, recorderActive, enabled);

        notice.Handle(new UmbracoApplicationStartingNotification(Umbraco.Cms.Core.RuntimeLevel.Run, false));

        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public void Retention_Negative_FailsStartUp()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["UserActivity:Recorder:RetentionDays"] = "-3" })
            .Build();
        var services = new ServiceCollection();
        services.AddOptions<UserActivityOptions>().Bind(config.GetSection(UserActivityOptions.SectionName)).ValidateOnStart();
        services.AddSingleton<IValidateOptions<UserActivityOptions>, UserActivityOptionsValidator>();
        ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException ex = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<UserActivityOptions>>().Value);

        Assert.Contains("UserActivity:Recorder:RetentionDays", ex.Message);
        Assert.DoesNotContain("-3", ex.Message);
    }

    [Fact]
    public void RetentionJob_RunsDaily_AndIsNamed()
    {
        var job = new RetentionJob(null!, new FixedOptions(new UserActivityOptions()), TimeProvider.System, new FakeLogger<RetentionJob>());

        Assert.Equal(TimeSpan.FromDays(1), job.Period);
        Assert.False(string.IsNullOrWhiteSpace(job.Name));
    }

    [Fact]
    public async Task RetentionJob_ZeroDays_IsANoOp_NeverOpensAScope()
    {
        var scopes = NSubstitute.Substitute.For<Umbraco.Cms.Infrastructure.Scoping.IScopeProvider>();
        var logger = new FakeLogger<RetentionJob>();
        var job = new RetentionJob(scopes, new FixedOptions(new UserActivityOptions()), TimeProvider.System, logger);

        await job.ExecuteAsync();

        Assert.Empty(scopes.ReceivedCalls());
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task RetentionJob_AbsurdDays_IsANoOp_NeverThrowsNorOpensAScope()
    {
        var scopes = NSubstitute.Substitute.For<Umbraco.Cms.Infrastructure.Scoping.IScopeProvider>();
        var logger = new FakeLogger<RetentionJob>();
        var options = new UserActivityOptions { Recorder = new RecorderOptions { RetentionDays = int.MaxValue } };
        var job = new RetentionJob(scopes, new FixedOptions(options), TimeProvider.System, logger);

        await job.ExecuteAsync();

        Assert.Empty(scopes.ReceivedCalls());
        Assert.Empty(logger.Collector.GetSnapshot());
    }

    private sealed class FixedOptions(UserActivityOptions value) : IOptionsMonitor<UserActivityOptions>
    {
        public UserActivityOptions CurrentValue => value;

        public UserActivityOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<UserActivityOptions, string?> listener) => null;
    }
}
