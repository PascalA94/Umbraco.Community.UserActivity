using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using RepositoryCacheMode = Umbraco.Cms.Core.Scoping.RepositoryCacheMode;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Tests.Recorder;

public class RecorderWriterTests
{
    private const string Sentinel = "SentinelPersonalData";

    private static RecorderEntry Entry(Dictionary<string, string>? details = null)
        => new("csp", Categories.Saved, "CspSavedNotification", Guid.NewGuid(), Sentinel + " Name", "cspPolicy", "key-1", Sentinel + " Policy", details);

    private static IScopeProvider ThrowingScopes(Exception exception)
    {
        IScopeProvider scopes = Substitute.For<IScopeProvider>();
        scopes.CreateScope(
                Arg.Any<IsolationLevel>(), Arg.Any<RepositoryCacheMode>(), Arg.Any<IEventDispatcher?>(),
                Arg.Any<IScopedNotificationPublisher?>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(_ => throw exception);
        return scopes;
    }

    [Fact]
    public void Writer_OwnScope_SwallowsAndLogsTypeOnly()
    {
        var logger = new FakeLogger<RecorderWriter>();
        IScopeProvider scopes = ThrowingScopes(new InvalidOperationException(Sentinel + " in the message"));
        var writer = new RecorderWriter(scopes, TimeProvider.System, new TestOptions(), logger);

        Exception? thrown = Record.Exception(() => writer.Write(Entry()));

        Assert.Null(thrown);
        FakeLogRecord warning = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("csp", warning.Message);
        Assert.Contains(nameof(InvalidOperationException), warning.Message);
        Assert.DoesNotContain(Sentinel, warning.Message);
        Assert.Null(warning.Exception);
        Assert.DoesNotContain(Sentinel, string.Concat(warning.StructuredState!.Select(p => p.Value)));
    }

    [Fact]
    public void Writer_AskedForAFreshScopeOnEveryWrite()
    {
        IScopeProvider scopes = ThrowingScopes(new InvalidOperationException());
        var writer = new RecorderWriter(scopes, TimeProvider.System, new TestOptions(), new FakeLogger<RecorderWriter>());

        writer.Write(Entry());
        writer.Write(Entry());

        scopes.Received(2).CreateScope(
            Arg.Any<IsolationLevel>(), Arg.Any<RepositoryCacheMode>(), Arg.Any<IEventDispatcher?>(),
            Arg.Any<IScopedNotificationPublisher?>(), Arg.Any<bool?>(), Arg.Any<bool>(), Arg.Any<bool>());
    }

    [Fact]
    public void Writer_RecorderSourceSwitchedOff_WritesNothing()
    {
        IScopeProvider scopes = Substitute.For<IScopeProvider>();
        var options = new TestOptions();
        options.CurrentValue.Sources[SourceAlias.Recorder] = new SourceOptions { Enabled = false };
        var writer = new RecorderWriter(scopes, TimeProvider.System, options, new FakeLogger<RecorderWriter>());

        writer.Write(Entry());

        Assert.Empty(scopes.ReceivedCalls());
    }

    [Fact]
    public void Writer_CapsFieldsAndDetails()
    {
        var details = new Dictionary<string, string> { ["policy"] = new string('p', 3000), ["other"] = new string('o', 3000), ["small"] = "kept" };
        var entry = new RecorderEntry(
            new string('s', 99), new string('c', 99), new string('r', 999), Guid.NewGuid(), new string('u', 999),
            new string('t', 999), new string('k', 999), new string('n', 999), details);

        ActivityEventDto dto = RecorderWriter.ToDto(entry, new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc));

        Assert.Equal(RecorderTable.SourceMax, dto.Source.Length);
        Assert.Equal(Categories.Other, dto.Category);
        Assert.Equal(RecorderTable.RawTypeMax, dto.RawType.Length);
        Assert.Equal(RecorderTable.UserNameMax, dto.UserName!.Length);
        Assert.Equal(RecorderTable.EntityTypeMax, dto.EntityType.Length);
        Assert.Equal(RecorderTable.EntityKeyMax, dto.EntityKey!.Length);
        Assert.Equal(RecorderTable.EntityNameMax, dto.EntityName!.Length);
        Assert.InRange(dto.DetailsJson!.Length, 1, RecorderTable.DetailsMax);
        // Whole keys are dropped rather than the JSON cut mid-way, so what is stored always parses.
        Dictionary<string, string> stored = JsonSerializer.Deserialize<Dictionary<string, string>>(dto.DetailsJson)!;
        Assert.Equal("kept", stored["small"]);
        Assert.Equal(new DateTime(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc), dto.OccurredUtc);
    }

    [Fact]
    public void Writer_NoDetails_StoresNull()
    {
        Assert.Null(RecorderWriter.ToDto(Entry(), DateTime.UtcNow).DetailsJson);
        Assert.Null(RecorderWriter.ToDto(Entry([]), DateTime.UtcNow).DetailsJson);
    }

    [Fact]
    public void Entry_ToString_NamesNoPersonalData()
        => Assert.DoesNotContain(Sentinel, Entry(new() { ["a"] = Sentinel }).ToString());

    private sealed class TestOptions : Microsoft.Extensions.Options.IOptionsMonitor<UserActivityOptions>
    {
        public UserActivityOptions CurrentValue { get; } = new();

        public UserActivityOptions Get(string? name) => CurrentValue;

        public IDisposable? OnChange(Action<UserActivityOptions, string?> listener) => null;
    }
}
