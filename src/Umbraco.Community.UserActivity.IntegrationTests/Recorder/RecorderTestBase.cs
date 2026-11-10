using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Recorder;

namespace Umbraco.Community.UserActivity.IntegrationTests.Recorder;

/// <summary>
/// A fresh site database per test with the recorder's migration plan applied, the way an install or upgrade does.
/// Runs unchanged on SQL Server when the release-checks job sets the <c>Tests__Database__*</c> variables.
/// </summary>
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public abstract class RecorderTestBase : UmbracoIntegrationTest
{
    protected IScopeProvider Scopes => GetRequiredService<IScopeProvider>();

    internal static TestOptionsMonitor<UserActivityOptions> Options(int retentionDays = 0)
        => new(new UserActivityOptions { Recorder = new RecorderOptions { RetentionDays = retentionDays } });

    protected async Task ApplyMigrationPlanAsync()
    {
        var plan = new RecorderMigrationPlan();
        await GetRequiredService<IMigrationPlanExecutor>().ExecutePlanAsync(plan, string.Empty);
    }

    internal RecorderWriter Writer(TimeProvider? time = null, TestOptionsMonitor<UserActivityOptions>? options = null)
        => new(Scopes, time ?? TimeProvider.System, options ?? Options(), new ListLogger<RecorderWriter>());

    internal static RecorderEntry Entry(string name = "Main policy", Guid? user = null, string category = "saved", string rawType = "CspSavedNotification", string entityType = "cspPolicy")
        => new("csp", category, rawType, user, user is null ? null : "Wren Writer", entityType, "e-" + name.Length, name, new Dictionary<string, string> { ["mode"] = "report" });

    protected int Count()
    {
        using IScope scope = Scopes.CreateScope(autoComplete: true);
        return scope.Database.ExecuteScalar<int>($"SELECT COUNT(*) FROM {scope.SqlContext.SqlSyntax.GetQuotedTableName(RecorderTable.Name)}");
    }
}

/// <summary>Records what is logged, so a test can assert on level, message and structured values.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => Entries.Add((logLevel, formatter(state, exception), exception));
}
