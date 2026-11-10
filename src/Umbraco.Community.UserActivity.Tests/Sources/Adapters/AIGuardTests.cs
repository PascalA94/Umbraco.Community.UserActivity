using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Community.UserActivity.Sources.Adapters.AI;

namespace Umbraco.Community.UserActivity.Tests.Sources.Adapters;

/// <summary>The AI guard without Umbraco.AI in the process (that is the AdapterTests project's job).</summary>
public class AIGuardTests
{
    [Fact]
    public void NotInstalled_IsNotInstalled()
    {
        SourceAvailabilityResult result = AIGuard.Evaluate(null);

        Assert.Equal(new SourceAvailabilityResult(SourceAlias.AI, AdapterState.NotInstalled, null, null), result);
    }

    [Fact]
    public void ProbeMissingMember_Unavailable()
    {
        // A stand-in "Umbraco.AI.Core" whose audit service lost GetAuditLogsPagedAsync.
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FakeAICore") { Version = new Version(17, 9, 0, 0) }, AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("FakeAICore");
        module.DefineType("Umbraco.AI.Core.AuditLog.IAIAuditLogService", TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract).CreateType();

        SourceAvailabilityResult result = AIGuard.Evaluate(assembly);

        Assert.Equal(AdapterState.ProbeFailed, result.State);
        Assert.Equal("17.9.0.0", result.AssemblyVersion);
        Assert.Equal("Umbraco.AI.Core.AuditLog.IAIAuditLogService.GetAuditLogsPagedAsync", result.MissingMember);
    }

    [Fact]
    public void Probe_CoversTheMembersS7Listed()
    {
        var names = AIGuard.Members.Select(m => m.DisplayName).ToList();

        Assert.Contains("Umbraco.AI.Core.AuditLog.IAIAuditLogService.GetAuditLogsPagedAsync", names);
        Assert.Contains("Umbraco.AI.Core.AuditLog.IAIAuditLogService.GetAuditLogAsync", names);
        foreach (var property in new[] { "Id", "StartTime", "UserId", "UserName", "EntityId", "EntityType", "FeatureType", "Status" })
        {
            Assert.Contains("Umbraco.AI.Core.AuditLog.AIAuditLog." + property, names);
        }

        foreach (var property in new[] { "UserId", "FromDate", "ToDate" })
        {
            Assert.Contains("Umbraco.AI.Core.AuditLog.AIAuditLogFilter." + property, names);
        }

        // Search is never passed to Umbraco.AI (SEC-F1), so its absence must not hide the source.
        Assert.DoesNotContain("Umbraco.AI.Core.AuditLog.AIAuditLogFilter.SearchText", names);

        Assert.Contains("Umbraco.AI.Core.AuditLog.AIAuditLogOptions.RetentionDays", names);
    }

    [Fact]
    public void ProbeFailure_LogsOneWarning_NamesOnly()
    {
        var registrations = new AdapterRegistrations();
        registrations.Add(new SourceAvailabilityResult(SourceAlias.AI, AdapterState.ProbeFailed, "17.9.0.0", "Umbraco.AI.Core.AuditLog.AIAuditLog.UserId"));
        registrations.Add(new SourceAvailabilityResult(SourceAlias.USync, AdapterState.NotInstalled, null, null));
        registrations.Add(new SourceAvailabilityResult("other", AdapterState.Registered, "1.0.0.0", null));
        var logger = new FakeLogger<SourceAvailability>();

        registrations.LogProblems(logger);

        FakeLogRecord warning = Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("ai", warning.Message);
        Assert.Contains("17.9.0.0", warning.Message);
        Assert.Contains("AIAuditLog.UserId", warning.Message);
    }
}

public class USyncGuardTests
{
    [Fact]
    public void NotInstalled_IsNotInstalled()
        => Assert.Equal(AdapterState.NotInstalled, Umbraco.Community.UserActivity.Sources.Adapters.USync.USyncGuard.Evaluate(null).State);

    [Fact]
    public void ProbeMissingMember_Unavailable()
    {
        // uSync.History 17.0.x: installed, but without ISyncHistoryService (S7).
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FakeUSyncHistory") { Version = new Version(17, 0, 4, 0) }, AssemblyBuilderAccess.Run);
        assembly.DefineDynamicModule("FakeUSyncHistory").DefineType("uSync.History.HistoryInfo", TypeAttributes.Public).CreateType();

        SourceAvailabilityResult result = Umbraco.Community.UserActivity.Sources.Adapters.USync.USyncGuard.Evaluate(assembly);

        Assert.Equal(new SourceAvailabilityResult(SourceAlias.USync, AdapterState.ProbeFailed, "17.0.4.0", "uSync.History.Service.ISyncHistoryService"), result);
    }
}
