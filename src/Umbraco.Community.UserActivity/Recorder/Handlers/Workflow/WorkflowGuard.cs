using System.Reflection;
using System.Runtime.CompilerServices;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Workflow.Core.ApprovalGroups.Notifications;
using Umbraco.Workflow.Core.Notifications;

namespace Umbraco.Community.UserActivity.Recorder.Handlers.Workflow;

/// <summary>
/// Decides at compose time whether Workflow's handlers are registered (design 3.2, 4.1). Strings and reflection only;
/// registration is a separate non-inlined method. Every notification used exists in every stable release on both lines
/// (M5 handler probe, docs/spikes.md); only the namespace of <c>IWorkflowInstance</c> differs between the lines, which
/// is why the probe names it per line (the handlers never name it).
/// </summary>
internal static class WorkflowGuard
{
    public const string AssemblyName = "Umbraco.Workflow.Core";

    private const string Core = "Umbraco.Workflow.Core.";
    private const string Instance =
#if UMBRACO_17
        Core + "Interfaces.IWorkflowInstance";
#else
        Core + "ContentApprovals.Interfaces.IWorkflowInstance";
#endif

    private const string GroupBase = Core + "ApprovalGroups.Interfaces.IWorkflowGroupBase";

    internal static IReadOnlyList<MemberSpec> Members { get; } =
    [
        MemberSpec.Type(Core + "Notifications.WorkflowSettingsSavedNotification"),
        MemberSpec.Type(Core + "ApprovalGroups.Notifications.WorkflowGroupCreatedNotification"),
        MemberSpec.Property(Core + "ApprovalGroups.Notifications.WorkflowGroupCreatedNotification", "CreatedEntity"),
        MemberSpec.Type(Core + "ApprovalGroups.Notifications.WorkflowGroupSavedNotification"),
        MemberSpec.Property(Core + "ApprovalGroups.Notifications.WorkflowGroupSavedNotification", "SavedEntities"),
        MemberSpec.Type(Core + "ApprovalGroups.Notifications.WorkflowGroupDeletedNotification"),
        MemberSpec.Property(Core + "ApprovalGroups.Notifications.WorkflowGroupDeletedNotification", "DeletedEntities"),
        MemberSpec.Property(GroupBase, "Name", "System.String"),
        MemberSpec.Property(GroupBase, "Unique", "System.Guid"),
        MemberSpec.Type(Core + "Notifications.WorkflowInstanceCreatedNotification"),
        MemberSpec.Property(Core + "Notifications.WorkflowInstanceCreatedNotification", "CreatedEntity"),
        MemberSpec.Type(Core + "Notifications.WorkflowInstanceApprovedNotification"),
        MemberSpec.Type(Core + "Notifications.WorkflowInstanceRejectedNotification"),
        MemberSpec.Property(Core + "Notifications.WorkflowInstanceUpdatedNotification", "UpdatedEntity", Instance),
        MemberSpec.Type(Core + "Notifications.WorkflowInstanceCompletedNotification"),
        MemberSpec.Property(Core + "Notifications.WorkflowInstanceCompletedNotification", "WorkflowType"),
        MemberSpec.Property(Instance, "WorkflowType"),
        MemberSpec.Type(Core + "ApprovalGroups.Notifications.WorkflowNodeConfigDeletedNotification"),
        MemberSpec.Type(Core + "ApprovalGroups.Notifications.WorkflowContentTypeConfigDeletedNotification"),
#if UMBRACO_18
        MemberSpec.Type(Core + "ApprovalGroups.Notifications.WorkflowConfigSavedNotification"),
#else
        MemberSpec.Type(Core + "ApprovalGroups.Notifications.WorkflowNodeConfigSavedNotification"),
        MemberSpec.Type(Core + "ApprovalGroups.Notifications.WorkflowContentTypeConfigSavedNotification"),
#endif
    ];

    public static SourceAvailabilityResult TryRegister(IUmbracoBuilder builder, RecorderHandlerRegistry handlers)
        => TryRegister(builder, handlers, OptionalAssemblyGuard.Find(AssemblyName));

    /// <summary>The assembly lookup is injectable so tests can drive both outcomes without the package being loaded.</summary>
    internal static SourceAvailabilityResult TryRegister(IUmbracoBuilder builder, RecorderHandlerRegistry handlers, Assembly? assembly)
    {
        SourceAvailabilityResult result = Evaluate(assembly);
        if (result.State == AdapterState.Registered)
        {
            Register(builder);
            handlers.Register(RecorderSources.Workflow);
        }

        return result;
    }

    internal static SourceAvailabilityResult Evaluate(Assembly? assembly) => RecorderGuardSupport.Evaluate(RecorderSources.Workflow, assembly, Members);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Register(IUmbracoBuilder builder)
    {
        builder.Services
            .AddRecorderHandler<WorkflowSettingsSavedNotification, WorkflowHandlers>(h => h.OnSettingsSaved)
            .AddRecorderHandler<WorkflowGroupCreatedNotification, WorkflowHandlers>(h => h.OnGroupCreated)
            .AddRecorderHandler<WorkflowGroupSavedNotification, WorkflowHandlers>(h => h.OnGroupSaved)
            .AddRecorderHandler<WorkflowGroupDeletedNotification, WorkflowHandlers>(h => h.OnGroupDeleted)
            .AddRecorderHandler<WorkflowInstanceCreatedNotification, WorkflowHandlers>(h => h.OnInstanceCreated)
            .AddRecorderHandler<WorkflowInstanceApprovedNotification, WorkflowHandlers>(h => h.OnInstanceApproved)
            .AddRecorderHandler<WorkflowInstanceRejectedNotification, WorkflowHandlers>(h => h.OnInstanceRejected)
            .AddRecorderHandler<WorkflowInstanceCompletedNotification, WorkflowHandlers>(h => h.OnInstanceCompleted)
            .AddRecorderHandler<WorkflowNodeConfigDeletedNotification, WorkflowHandlers>(h => h.OnNodeConfigDeleted)
            .AddRecorderHandler<WorkflowContentTypeConfigDeletedNotification, WorkflowHandlers>(h => h.OnContentTypeConfigDeleted);
#if UMBRACO_18
        builder.Services.AddRecorderHandler<WorkflowConfigSavedNotification, WorkflowHandlers>(h => h.OnConfigSaved);
#else
        builder.Services
            .AddRecorderHandler<WorkflowNodeConfigSavedNotification, WorkflowHandlers>(h => h.OnNodeConfigSaved)
            .AddRecorderHandler<WorkflowContentTypeConfigSavedNotification, WorkflowHandlers>(h => h.OnContentTypeConfigSaved);
#endif
    }
}
