using Umbraco.Community.UserActivity.Sources;
using Umbraco.Workflow.Core.ApprovalGroups.Notifications;
using Umbraco.Workflow.Core.Notifications;

namespace Umbraco.Community.UserActivity.Recorder.Handlers.Workflow;

/// <summary>
/// Workflow's settings, approval groups and workflow instances (design 4.1). The entity types differ between the two
/// lines (<c>IWorkflowInstance</c> moved namespace), so nothing here names them: members are read through <c>var</c>.
/// No generic method here is constrained to a Workflow type: a constraint makes the type fail to load without Workflow
/// (ArchitectureTests_EveryTypeLoads_WithoutOptionalAssemblies).
/// Instance events record only the workflow type (publish, unpublish, ...); the author's comment, the page and any
/// approver comments are never read, because they are free text written by people.
/// Approved and rejected are raised per step, completed once at the end: S4 saw a one-step approval raise only
/// completed, so a final approval can store a step row and a completed row, each under its own raw type.
/// </summary>
internal sealed class WorkflowHandlers
{
    private const string ConfigType = "workflowConfig";
    private const string InstanceType = "workflowInstance";

    private readonly RecorderHandlerContext _context;

    public WorkflowHandlers(RecorderHandlerContext context) => _context = context;

    public void OnSettingsSaved(WorkflowSettingsSavedNotification notification)
        => _context.Record(RecorderSources.Workflow, () => [Config(Categories.Saved, nameof(WorkflowSettingsSavedNotification), null, "Workflow settings")]);

    public void OnGroupSaved(WorkflowGroupSavedNotification notification)
        => _context.Record(RecorderSources.Workflow, () => RecorderDetails.Capped(
            notification.SavedEntities,
            group => Config(Categories.Saved, nameof(WorkflowGroupSavedNotification), group.Unique.ToString(), group.Name),
            dropped => Truncated(Categories.Saved, nameof(WorkflowGroupSavedNotification), dropped)));

    public void OnGroupDeleted(WorkflowGroupDeletedNotification notification)
        => _context.Record(RecorderSources.Workflow, () => RecorderDetails.Capped(
            notification.DeletedEntities,
            group => Config(Categories.Deleted, nameof(WorkflowGroupDeletedNotification), group.Unique.ToString(), group.Name),
            dropped => Truncated(Categories.Deleted, nameof(WorkflowGroupDeletedNotification), dropped)));

#if UMBRACO_18
    public void OnConfigSaved(WorkflowConfigSavedNotification notification)
        => _context.Record(RecorderSources.Workflow, () => [Config(Categories.Saved, nameof(WorkflowConfigSavedNotification), null, "Workflow configuration")]);
#endif

    public void OnInstanceCreated(WorkflowInstanceCreatedNotification notification)
        => _context.Record(RecorderSources.Workflow, () => [Instance(Categories.Created, nameof(WorkflowInstanceCreatedNotification), notification.CreatedEntity.WorkflowType.ToString())]);

    public void OnInstanceApproved(WorkflowInstanceApprovedNotification notification)
        => _context.Record(RecorderSources.Workflow, () => [Instance(Categories.Other, nameof(WorkflowInstanceApprovedNotification), notification.UpdatedEntity.WorkflowType.ToString())]);

    public void OnInstanceRejected(WorkflowInstanceRejectedNotification notification)
        => _context.Record(RecorderSources.Workflow, () => [Instance(Categories.Other, nameof(WorkflowInstanceRejectedNotification), notification.UpdatedEntity.WorkflowType.ToString())]);

    public void OnInstanceCompleted(WorkflowInstanceCompletedNotification notification)
        => _context.Record(RecorderSources.Workflow, () => [Instance(Categories.Other, nameof(WorkflowInstanceCompletedNotification), notification.WorkflowType.ToString())]);

    private static RecorderEntry Config(string category, string rawType, string? key, string? name)
        => new(RecorderSources.Workflow, category, rawType, null, null, ConfigType, key, name, null);

    private static RecorderEntry Truncated(string category, string rawType, int dropped)
        => new(RecorderSources.Workflow, category, rawType, null, null, ConfigType, null, null, RecorderDetails.Truncated(dropped));

    private static RecorderEntry Instance(string category, string rawType, string workflowType)
        => new(RecorderSources.Workflow, category, rawType, null, null, InstanceType, null, null, new Dictionary<string, string> { ["workflowType"] = workflowType });
}
