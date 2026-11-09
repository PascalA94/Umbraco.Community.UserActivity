using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Security;
using Umbraco.Community.UserActivity.Tests.Sources;

namespace Umbraco.Community.UserActivity.Tests.Security;

public class ViewLoggerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);
    private readonly IAuditEntryService _audit = Substitute.For<IAuditEntryService>();
    private readonly FakeTimeProvider _time = new(T0);
    private readonly UserActivityOptions _options = new();
    private readonly FakeLogger<ViewLogger> _logger = new();
    private readonly Guid _viewer = Guid.NewGuid();
    private readonly Guid _subject = Guid.NewGuid();
    private readonly ViewThrottle _throttle = new();

    // ViewLogger is transient and ViewThrottle a singleton: new loggers share one throttle.
    private ViewLogger Logger() => new(_audit, new StaticOptionsMonitor<UserActivityOptions>(_options), _time, _throttle, _logger);

    private Task<int> Writes(string eventType)
        => Task.FromResult(_audit.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IAuditEntryService.WriteAsync) && (string)c.GetArguments()[6]! == eventType));

    [Fact]
    public async Task ViewLogger_TabOpen_OneRowPerHour()
    {
        ViewLogger logger = Logger();

        await logger.LogViewAsync(_viewer, _subject, CancellationToken.None);
        _time.Advance(TimeSpan.FromMinutes(30));
        await logger.LogViewAsync(_viewer, _subject, CancellationToken.None);
        await Logger().LogViewAsync(_viewer, _subject, CancellationToken.None);
        Assert.Equal(1, await Writes(ViewLogger.ViewedEventType));

        await logger.LogViewAsync(_viewer, Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(2, await Writes(ViewLogger.ViewedEventType));

        _time.Advance(TimeSpan.FromMinutes(31));
        await logger.LogViewAsync(_viewer, _subject, CancellationToken.None);
        Assert.Equal(3, await Writes(ViewLogger.ViewedEventType));
        // Core's AuditEntryService rejects blank performer and event details.
        // No viewer IP is stored (data minimisation, M1 review); core's blank-details check is honoured.
        await _audit.Received().WriteAsync(_viewer, Arg.Is<string>(d => !string.IsNullOrWhiteSpace(d)), string.Empty, T0.AddMinutes(61).UtcDateTime, _subject, Arg.Any<string>(), ViewLogger.ViewedEventType, Arg.Is<string>(d => !string.IsNullOrWhiteSpace(d)));
    }

    [Fact]
    public async Task ViewLogger_Export_AlwaysLogs()
    {
        ViewLogger logger = Logger();

        await logger.LogExportAsync(_viewer, _subject, 12, CancellationToken.None);
        await logger.LogExportAsync(_viewer, _subject, 12, CancellationToken.None);
        await logger.LogExportAsync(_viewer, null, 3, CancellationToken.None);

        Assert.Equal(3, await Writes(ViewLogger.ExportedEventType));
    }

    [Fact]
    public async Task ViewLogger_Off_NoRows()
    {
        _options.LogViews = false;
        ViewLogger logger = Logger();

        await logger.LogViewAsync(_viewer, _subject, CancellationToken.None);
        await logger.LogExportAsync(_viewer, _subject, 1, CancellationToken.None);

        Assert.Empty(_audit.ReceivedCalls());
    }

    [Fact]
    public async Task ViewLogger_WriteFails_ReleasesThrottle()
    {
        // SEC-4: a failed write must not hide the next hour of views of the same subject.
        _audit.WriteAsync(default, default!, default!, default, default, default!, default!, default!)
            .ReturnsForAnyArgs(Task.FromException<Umbraco.Cms.Core.Models.IAuditEntry>(new InvalidOperationException()), Task.FromResult(Substitute.For<Umbraco.Cms.Core.Models.IAuditEntry>()));

        await Logger().LogViewAsync(_viewer, _subject, CancellationToken.None);
        await Logger().LogViewAsync(_viewer, _subject, CancellationToken.None);

        Assert.Equal(2, await Writes(ViewLogger.ViewedEventType));
    }

    [Fact]
    public async Task ViewLogger_GlobalView_OneRowPerHourWithNoSubject()
    {
        await Logger().LogGlobalViewAsync(_viewer, CancellationToken.None);
        await Logger().LogGlobalViewAsync(_viewer, CancellationToken.None);

        Assert.Equal(1, await Writes(ViewLogger.ViewedEventType));
        await _audit.Received(1).WriteAsync(_viewer, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<DateTime>(), null, Arg.Any<string>(), ViewLogger.ViewedEventType, Arg.Any<string>());
    }

    [Fact]
    public async Task ViewLogger_WriteFails_RequestGoesOn_TypeOnlyWarning()
    {
        _audit.WriteAsync(default, default!, default!, default, default, default!, default!, default!)
            .ReturnsForAnyArgs(Task.FromException<Umbraco.Cms.Core.Models.IAuditEntry>(new InvalidOperationException("sentinel")));

        await Logger().LogViewAsync(_viewer, _subject, CancellationToken.None);

        var record = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Contains(nameof(InvalidOperationException), record.Message);
        Assert.DoesNotContain("sentinel", record.Message);
        Assert.Null(record.Exception);
    }
}
