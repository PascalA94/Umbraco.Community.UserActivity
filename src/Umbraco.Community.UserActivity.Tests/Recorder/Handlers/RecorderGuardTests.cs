using System.Reflection;
using System.Reflection.Emit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Recorder.Handlers.CspManager;
using Umbraco.Community.UserActivity.Recorder.Handlers.SkybrudRedirects;
using Umbraco.Community.UserActivity.Recorder.Handlers.Workflow;
using Umbraco.Community.UserActivity.Sources.Adapters;
#if UMBRACO_17
using Umbraco.Community.UserActivity.Recorder.Handlers.UrlTracker;
#endif

namespace Umbraco.Community.UserActivity.Tests.Recorder.Handlers;

/// <summary>
/// The recorder handler guards without any recorded package in the process (the AdapterTests project binds the real
/// ones). Each guard must say "not installed" for a missing assembly, name the first missing member for an assembly
/// that changed, and register nothing in either case.
/// </summary>
public class RecorderGuardTests
{
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string> { RecorderSources.Csp, RecorderSources.Workflow, RecorderSources.SkybrudRedirects };
#if UMBRACO_17
        data.Add(RecorderSources.UrlTracker);
#endif
        return data;
    }

    private static (string AssemblyName, Func<Assembly?, SourceAvailabilityResult> Evaluate, IReadOnlyList<MemberSpec> Members) Guard(string name)
        => name switch
        {
            RecorderSources.Csp => (CspManagerGuard.AssemblyName, CspManagerGuard.Evaluate, CspManagerGuard.Members),
            RecorderSources.Workflow => (WorkflowGuard.AssemblyName, WorkflowGuard.Evaluate, WorkflowGuard.Members),
            RecorderSources.SkybrudRedirects => (SkybrudRedirectsGuard.AssemblyName, SkybrudRedirectsGuard.Evaluate, SkybrudRedirectsGuard.Members),
#if UMBRACO_17
            RecorderSources.UrlTracker => (UrlTrackerGuard.AssemblyName, UrlTrackerGuard.Evaluate, UrlTrackerGuard.Members),
#endif
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };

    [Theory]
    [MemberData(nameof(Names))]
    public void NotInstalled_IsNotInstalled(string name)
    {
        var guard = Guard(name);

        Assert.NotEmpty(guard.AssemblyName);
        Assert.NotEmpty(guard.Members);
        Assert.Equal(new SourceAvailabilityResult(name, AdapterState.NotInstalled, null, null), guard.Evaluate(null));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ProbeMissingMember_Unavailable_NamingTheFirstMissingMember(string name)
    {
        var guard = Guard(name);

        // A stand-in assembly with the right name and nothing in it: the first thing the probe wants is missing.
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(guard.AssemblyName) { Version = new Version(9, 9, 0, 0) }, AssemblyBuilderAccess.Run);
        assembly.DefineDynamicModule(guard.AssemblyName);

        SourceAvailabilityResult result = guard.Evaluate(assembly);

        Assert.Equal(new SourceAvailabilityResult(name, AdapterState.ProbeFailed, "9.9.0.0", guard.Members[0].DisplayName), result);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void NotInstalled_RegistersNothing(string name)
    {
        var services = new ServiceCollection();
        IUmbracoBuilder builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(services);
        var registry = new RecorderHandlerRegistry();

        SourceAvailabilityResult[] results =
        [
            CspManagerGuard.TryRegister(builder, registry),
            WorkflowGuard.TryRegister(builder, registry),
            SkybrudRedirectsGuard.TryRegister(builder, registry),
#if UMBRACO_17
            UrlTrackerGuard.TryRegister(builder, registry),
#endif
        ];

        Assert.Contains(results, r => r.Alias == name && r.State == AdapterState.NotInstalled);
        Assert.False(registry.Any);
        Assert.Empty(services);
    }

    [Fact]
    public void CspProbe_CoversTheMembersTheHandlerReads()
    {
        var names = CspManagerGuard.Members.Select(m => m.DisplayName).ToList();

        Assert.Contains("Umbraco.Community.CSPManager.Notifications.CspSavedNotification.CspDefinition", names);
        foreach (var property in new[] { "Id", "Enabled", "ReportOnly", "IsBackOffice", "Sources" })
        {
            Assert.Contains("Umbraco.Community.CSPManager.Models.CspDefinition." + property, names);
        }
    }

    [Fact]
    public void SkybrudProbe_CoversTheThreeRedirectNotificationsAndTheFieldsRead()
    {
        var names = SkybrudRedirectsGuard.Members.Select(m => m.DisplayName).ToList();

        foreach (var notification in new[] { "RedirectAddedNotification", "RedirectSavedNotification", "RedirectDeletedNotification" })
        {
            Assert.Contains("Skybrud.Umbraco.Redirects.Notifications." + notification + ".Redirect", names);
        }

        foreach (var property in new[] { "Key", "Path", "Type", "IsPermanent" })
        {
            Assert.Contains("Skybrud.Umbraco.Redirects.Models.IRedirect." + property, names);
        }

        // The query string and the destination are never read, so their absence must not hide the source.
        Assert.DoesNotContain("Skybrud.Umbraco.Redirects.Models.IRedirect.QueryString", names);
        Assert.DoesNotContain("Skybrud.Umbraco.Redirects.Models.IRedirect.Url", names);
    }

    [Fact]
    public void WorkflowProbe_CoversTheNotificationsAndTheGroupFieldsRead()
    {
        var names = WorkflowGuard.Members.Select(m => m.DisplayName).ToList();

        foreach (var type in new[]
        {
            "Notifications.WorkflowSettingsSavedNotification", "ApprovalGroups.Notifications.WorkflowGroupSavedNotification",
            "ApprovalGroups.Notifications.WorkflowGroupDeletedNotification", "Notifications.WorkflowInstanceCreatedNotification",
            "Notifications.WorkflowInstanceApprovedNotification", "Notifications.WorkflowInstanceRejectedNotification",
            "Notifications.WorkflowInstanceCompletedNotification",
        })
        {
            Assert.Contains("Umbraco.Workflow.Core." + type, names);
        }

        Assert.Contains("Umbraco.Workflow.Core.ApprovalGroups.Interfaces.IWorkflowGroupBase.Name", names);
        Assert.Contains("Umbraco.Workflow.Core.ApprovalGroups.Interfaces.IWorkflowGroupBase.Unique", names);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void ProbeFailed_RegistersNothing_AndLeavesTheRegistryEmpty(string name)
    {
        var guard = Guard(name);
        var services = new ServiceCollection();
        IUmbracoBuilder builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(services);
        var registry = new RecorderHandlerRegistry();

        // The recorder source stays hidden until a handler registered (RecorderSource.IsAvailable), so a failed probe
        // must leave the registry empty and add no service.
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(guard.AssemblyName), AssemblyBuilderAccess.Run);
        assembly.DefineDynamicModule(guard.AssemblyName);

        SourceAvailabilityResult result = TryRegister(name, builder, registry, assembly);

        Assert.Equal(AdapterState.ProbeFailed, result.State);
        Assert.False(registry.Any);
        Assert.Empty(registry.Names);
        Assert.Empty(services);
    }

    private static SourceAvailabilityResult TryRegister(string name, IUmbracoBuilder builder, RecorderHandlerRegistry registry, Assembly? assembly)
        => name switch
        {
            RecorderSources.Csp => CspManagerGuard.TryRegister(builder, registry, assembly),
            RecorderSources.Workflow => WorkflowGuard.TryRegister(builder, registry, assembly),
            RecorderSources.SkybrudRedirects => SkybrudRedirectsGuard.TryRegister(builder, registry, assembly),
#if UMBRACO_17
            RecorderSources.UrlTracker => UrlTrackerGuard.TryRegister(builder, registry, assembly),
#endif
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
}
