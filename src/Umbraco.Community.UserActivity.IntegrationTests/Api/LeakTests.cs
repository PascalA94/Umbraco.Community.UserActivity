using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.IntegrationTests.Core;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.IntegrationTests.Api;

/// <summary>
/// Design 7.4 / plan Task 13: sentinel personal data seeded into every field and sent through every API path and
/// failure path never reaches our logs, and never comes back in an error body.
/// </summary>
[TestFixture]
public class LeakTests : ApiTestBase
{
    private const string Name = "Sentinelname Person";
    private const string Email = "sentinel.person@example.test";
    private const string Ip = "198.51.100.77";
    private const string Item = "SentinelItemName";
    private const string Search = "SentinelSearchText";
    private const string Details = "SentinelEventDetails";
    private static readonly string[] Sentinels = ["Sentinel", "sentinel", Ip];

    private static readonly CapturingLoggerProvider Captured = new();

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        // Umbraco's Serilog factory ignores extra providers, so capture at ILogger<T>: every typed logger in the
        // site (ours and core's) writes here.
        builder.Services.AddSingleton(Captured);
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(CapturingLogger<>));
        builder.UserActivitySources().Append<LeakySource>();
    }

    [Test]
    public async Task LeakTests_NoPersonalDataInLogs()
    {
        UserActivityOptions options = Services.GetRequiredService<IOptionsMonitor<UserActivityOptions>>().CurrentValue;
        options.Export.Enabled = true;
        options.ShowIpAddresses = true;
        IScopeProvider scopes = Services.GetRequiredService<IScopeProvider>();
        IUser admin = await Users.CreateAdminAsync("Admin");
        IUser person = await Users.CreateAsync(Name, ["writer"], Email);
        CoreSeed.Log(scopes, person.Id, 987654, "Document", "Save", Item + " " + Details, DateTime.UtcNow.AddMinutes(-1), parameters: Details);
        CoreSeed.Audit(scopes, person.Id, person.Key, "umbraco/user/save", DateTime.UtcNow.AddMinutes(-1), eventDetails: Details, performingIp: Ip, performingDetails: $"{Name} <{Email}>", affectedDetails: $"{Name} <{Email}>");
        CoreSeed.Audit(scopes, -1, Umbraco.Cms.Core.Constants.Security.SuperUserKey, "umbraco/user/sign-in/failed", DateTime.UtcNow.AddMinutes(-1), eventDetails: Email, performingIp: Ip);
        Captured.Clear();
        HttpClient client = await CreateAuthenticatedClientAsync(admin);
        const string api = ManagementApi + "user-activity/";

        string list = await client.GetStringAsync(api + $"events?search={Search}");
        string page = await client.GetStringAsync(api + $"users/{person.Key}/events");
        var bodies = new List<string>();
        foreach (var id in System.Text.RegularExpressions.Regex.Matches(page, "\"id\":\"(?<id>[a-z]+:\\d+)\"").Select(m => m.Groups["id"].Value))
        {
            await client.GetAsync(api + $"events/core/{Uri.EscapeDataString(id)}");
        }

        await client.GetAsync(api + "filter-options");
        await client.GetAsync(api + $"events/export?userKey={person.Key}");
        foreach (var bad in new[]
                 {
                     $"events?search={Search}{new string('x', 120)}", $"events?source={Search}", $"events?category={Email}",
                     $"events?entityType={Item}", $"events?from={Search}", $"events?userKey={Search}", $"events?take={Search}",
                     $"events/leaky/{Search}", $"events/core/{Search}", $"users/{Guid.NewGuid()}/events?search={Search}",
                 })
        {
            HttpResponseMessage response = await client.GetAsync(api + bad);
            Assert.That((int)response.StatusCode, Is.LessThan(500), bad);
            bodies.Add(await response.Content.ReadAsStringAsync());
        }

        Assert.That(list, Does.Contain("leaky").And.Contain("failed"), "the leaky source ran and failed");
        var ours = Captured.Records.Where(r => r.Category.StartsWith("Umbraco.Community.UserActivity", StringComparison.Ordinal)).ToList();
        Assert.That(ours, Is.Not.Empty, "the failing source must have logged a warning");
        foreach (var record in Captured.Records.Where(r => r.Category.StartsWith("Umbraco.Community.UserActivity", StringComparison.Ordinal) || r.Exception is not null))
        {
            foreach (var sentinel in Sentinels)
            {
                Assert.That(record.Message, Does.Not.Contain(sentinel), $"{record.Category}: {record.Message}");
                Assert.That(record.Exception?.ToString() ?? string.Empty, Does.Not.Contain(sentinel), record.Category);
            }
        }

        foreach (var body in bodies)
        {
            Assert.That(body, Does.Not.Contain("Sentinel").And.Not.Contain("sentinel"));
        }
    }

    /// <summary>A source whose failure message is full of personal data; only its exception type may be logged.</summary>
    public sealed class LeakySource : IUserActivitySource
    {
        public string Alias => "leaky";

        public string DisplayNameKey => "userActivity_sourceLeaky";

        public bool IsAvailable => true;

        public ActivitySourceInfo Info { get; } = new([], Categories.All, true, true, null);

        public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
            => throw new InvalidOperationException($"{Name} {Email} {Ip} {query.Search} {query.User?.Username}");

        public Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
            => throw new InvalidOperationException($"{Name} {Email} {Ip} {id}");
    }

    public sealed class CapturingLoggerProvider
    {
        private readonly ConcurrentQueue<Record> _records = new();

        public IEnumerable<Record> Records => _records;

        public void Clear() => _records.Clear();

        public void Add(Record record) => _records.Enqueue(record);
    }

    public sealed record Record(string Category, LogLevel Level, string Message, Exception? Exception);

    public sealed class CapturingLogger<T>(CapturingLoggerProvider records) : ILogger<T>
    {
        private readonly string _category = typeof(T).FullName ?? typeof(T).Name;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => records.Add(new Record(_category, logLevel, formatter(state, exception), exception));
    }
}
