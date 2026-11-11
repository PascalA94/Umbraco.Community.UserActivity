using System.Reflection;
using NSubstitute;
using Umbraco.Cms.Core.Events;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Recorder.Handlers;
using Umbraco.Community.UserActivity.Recorder.Handlers.Workflow;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Sources.Adapters;
using Umbraco.Workflow.Core.ApprovalGroups.Interfaces;
using Umbraco.Workflow.Core.ApprovalGroups.Notifications;
using Umbraco.Workflow.Core.Notifications;
#if UMBRACO_17
using Umbraco.Workflow.Core.Interfaces;
using Umbraco.Workflow.Core.Models.Enums;
#else
using Umbraco.Workflow.Core.Common.Interfaces;
using Umbraco.Workflow.Core.ContentApprovals.Interfaces;
using Umbraco.Workflow.Core.ContentApprovals.Models;
using Umbraco.Workflow.Core.Settings;
#endif

namespace Umbraco.Community.UserActivity.AdapterTests.Recorder;

/// <summary>
/// The Workflow handlers bound to a real Umbraco.Workflow.Core at <c>WorkflowRuntimeVersion</c> (M5 Task 33). The
/// notifications are built the way Workflow builds them, with substitutes for its entity interfaces.
/// </summary>
public class WorkflowHandlerTests
{
    private const string Sentinel = "SentinelComment";
    private static readonly Guid GroupKey = Guid.Parse("5e0c0000-0000-4000-8000-0000000000ee");
    private static readonly EventMessages Messages = new();

    private static HandlerHarness Harness(bool withUser = true, Exception? writerFailure = null)
        => HandlerHarness.Create(WorkflowGuard.TryRegister, withUser, writerFailure);

    private static IWorkflowGroup Group(string name = "Editors")
    {
        IWorkflowGroup group = Substitute.For<IWorkflowGroup>();
        group.Unique.Returns(GroupKey);
        group.Name.Returns(name);
        group.GroupEmail.Returns(Sentinel + "@example.test");
        return group;
    }

    private static IWorkflowInstance Instance()
    {
        IWorkflowInstance instance = Substitute.For<IWorkflowInstance>();
        instance.WorkflowType.Returns((WorkflowType)Enum.GetValues<WorkflowType>().GetValue(0)!);
        return instance;
    }

    private static string InstanceTypeName() => Enum.GetValues<WorkflowType>()[0].ToString();

    [Fact]
    public void RealAssembly_IsTheRuntimeVersion_AndTheProbePasses()
    {
        var assembly = typeof(WorkflowSettingsSavedNotification).Assembly;

        HandlerHarness.AssertIsRuntimeVersion(assembly, "Workflow");
        Assert.Equal(AdapterState.Registered, WorkflowGuard.Evaluate(assembly).State);
    }

    [Fact]
    public void PassingAssembly_InjectedIntoTheGuard_Registers()
    {
        HandlerHarness harness = HandlerHarness.Create((builder, registry) => WorkflowGuard.TryRegister(builder, registry, typeof(WorkflowSettingsSavedNotification).Assembly));

        Assert.Equal(AdapterState.Registered, harness.Result.State);
        Assert.Equal([RecorderSources.Workflow], harness.Registry.Names);
        Assert.True(harness.Registry.Any);
    }

    [Fact]
    public void Installed_RegistersEveryHandlerAndTheRecorderName()
    {
        HandlerHarness harness = Harness();

        Assert.Equal(AdapterState.Registered, harness.Result.State);
        Assert.Equal([RecorderSources.Workflow], harness.Registry.Names);
        Assert.Equal(1, harness.HandlerCount<WorkflowSettingsSavedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowGroupCreatedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowGroupSavedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowGroupDeletedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowInstanceCreatedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowInstanceApprovedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowInstanceRejectedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowInstanceCompletedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowNodeConfigDeletedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowContentTypeConfigDeletedNotification>());
#if UMBRACO_18
        Assert.Equal(1, harness.HandlerCount<WorkflowConfigSavedNotification>());
#else
        Assert.Equal(1, harness.HandlerCount<WorkflowNodeConfigSavedNotification>());
        Assert.Equal(1, harness.HandlerCount<WorkflowContentTypeConfigSavedNotification>());
#endif
    }

    [Fact]
    public void SettingsSaved_IsExactlyOneRow_WithTheBackOfficeUser()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowSettingsSavedNotification(Substitute.For<ISettings>(), Messages));

        RecorderEntry row = Assert.Single(harness.Written);
        Assert.Equal(RecorderSources.Workflow, row.Source);
        Assert.Equal(Categories.Saved, row.Category);
        Assert.Equal(nameof(WorkflowSettingsSavedNotification), row.RawType);
        Assert.Equal("workflowConfig", row.EntityType);
        Assert.Equal("Workflow settings", row.EntityName);
        Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
        Assert.Equal("Wren Writer", row.UserName);
    }

    // Found in the real-package exercise: creating a group raises WorkflowGroupCreatedNotification only; Saved fires on edit.
    [Fact]
    public void GroupCreated_IsExactlyOneRow_NamedAfterTheGroup_AndNeverItsEmail()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowGroupCreatedNotification(Group("New reviewers"), Messages));

        RecorderEntry row = Assert.Single(harness.Written);
        Assert.Equal(Categories.Created, row.Category);
        Assert.Equal(nameof(WorkflowGroupCreatedNotification), row.RawType);
        Assert.Equal("workflowConfig", row.EntityType);
        Assert.Equal(GroupKey.ToString(), row.EntityKey);
        Assert.Equal("New reviewers", row.EntityName);
        Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
        Assert.Null(row.Details);
    }

    // Found in the real-package exercise: 17 raised these on every approval-flow save and nothing was recorded, and
    // neither line recorded a config delete.
    [Fact]
    public void ConfigDeleted_IsOneRowEach_ForNodeAndDocumentTypeFlows_WithNoDetails()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowNodeConfigDeletedNotification(Substitute.For<IWorkflowConfig>(), Messages));
        harness.Publish(new WorkflowContentTypeConfigDeletedNotification(Substitute.For<IWorkflowConfig>(), Messages));

        Assert.Equal(
            [nameof(WorkflowNodeConfigDeletedNotification), nameof(WorkflowContentTypeConfigDeletedNotification)],
            harness.Written.Select(r => r.RawType));
        Assert.All(harness.Written, row =>
        {
            Assert.Equal(Categories.Deleted, row.Category);
            Assert.Equal("workflowConfig", row.EntityType);
            Assert.Equal("Workflow configuration", row.EntityName);
            Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
            Assert.Null(row.Details);
        });
    }

#if UMBRACO_17
    [Fact]
    public void ConfigSaved_On17_IsOneRowEach_ForNodeAndDocumentTypeFlows_WithNoDetails()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowNodeConfigSavedNotification(Substitute.For<IWorkflowConfig>(), Messages));
        harness.Publish(new WorkflowContentTypeConfigSavedNotification(Substitute.For<IWorkflowConfig>(), Messages));

        Assert.Equal(
            [nameof(WorkflowNodeConfigSavedNotification), nameof(WorkflowContentTypeConfigSavedNotification)],
            harness.Written.Select(r => r.RawType));
        Assert.All(harness.Written, row =>
        {
            Assert.Equal(Categories.Saved, row.Category);
            Assert.Equal("workflowConfig", row.EntityType);
            Assert.Equal("Workflow configuration", row.EntityName);
            Assert.Null(row.Details);
        });
    }
#endif

    [Fact]
    public void GroupSaved_AndDeleted_AreExactlyOneRowEach()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowGroupSavedNotification(Group(), Messages));
        harness.Publish(new WorkflowGroupDeletedNotification(Group("Reviewers"), Messages));

        Assert.Equal(
            [(Categories.Saved, nameof(WorkflowGroupSavedNotification), "Editors"), (Categories.Deleted, nameof(WorkflowGroupDeletedNotification), "Reviewers")],
            harness.Written.Select(r => (r.Category, r.RawType, r.EntityName!)));
        Assert.All(harness.Written, row =>
        {
            Assert.Equal("workflowConfig", row.EntityType);
            Assert.Equal(GroupKey.ToString(), row.EntityKey);
            Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
        });
    }

    [Fact]
    public void InstanceCreated_IsExactlyOneRow_WithOnlyTheWorkflowType()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowInstanceCreatedNotification(Instance(), Messages));

        RecorderEntry row = Assert.Single(harness.Written);
        Assert.Equal(Categories.Created, row.Category);
        Assert.Equal("workflowInstance", row.EntityType);
        Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
        HandlerHarness.AssertOnlyKeys(row, "workflowType");
        Assert.Equal(InstanceTypeName(), row.Details!["workflowType"]);
    }

    [Fact]
    public void InstanceApproved_Rejected_AndCompleted_AreExactlyOneRowEach()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowInstanceApprovedNotification(Instance(), Messages));
        harness.Publish(new WorkflowInstanceRejectedNotification(Instance(), Messages));
        harness.Publish(new WorkflowInstanceCompletedNotification(Instance(), Messages));

        Assert.Equal(
            [nameof(WorkflowInstanceApprovedNotification), nameof(WorkflowInstanceRejectedNotification), nameof(WorkflowInstanceCompletedNotification)],
            harness.Written.Select(r => r.RawType));
        Assert.All(harness.Written, row =>
        {
            Assert.Equal(Categories.Other, row.Category);
            Assert.Equal("workflowInstance", row.EntityType);
            Assert.Equal(HandlerHarness.WrenKey, row.UserKey);
            HandlerHarness.AssertOnlyKeys(row, "workflowType");
        });
    }

    // Workflow's notifications only expose a single-entity constructor, but the CMS base class carries a collection and a
    // real bulk save raises one with several: replace that collection the way the base constructor would have set it.
    private static TNotification WithEntities<TNotification>(TNotification notification, IWorkflowGroup[] groups)
        where TNotification : class
    {
        FieldInfo? field = null;
        for (Type? type = typeof(TNotification); field is null && type is not null; type = type.BaseType)
        {
            field = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .SingleOrDefault(f => f.FieldType == typeof(IEnumerable<IWorkflowGroup>));
        }

        field!.SetValue(notification, groups);
        return notification;
    }

    [Fact]
    public void GroupSaved_AndDeleted_OverTheCap_WriteOneTruncatedRowEach()
    {
        HandlerHarness harness = Harness();
        IWorkflowGroup[] many = Enumerable.Range(0, RecorderDetails.MaxRowsPerNotification + 7).Select(i => Group("G" + i)).ToArray();

        harness.Publish(WithEntities(new WorkflowGroupSavedNotification(many[0], Messages), many));
        List<RecorderEntry> saved = harness.Written.ToList();
        harness.Written.Clear();
        harness.Publish(WithEntities(new WorkflowGroupDeletedNotification(many[0], Messages), many));

        foreach (List<RecorderEntry> rows in new[] { saved, harness.Written })
        {
            Assert.Equal(RecorderDetails.MaxRowsPerNotification + 1, rows.Count);
            RecorderEntry marker = rows[^1];
            HandlerHarness.AssertOnlyKeys(marker, "truncated");
            Assert.Equal("7", marker.Details!["truncated"]);
            Assert.Equal("workflowConfig", marker.EntityType);
        }

        Assert.Equal(Categories.Saved, saved[^1].Category);
        Assert.Equal(Categories.Deleted, harness.Written[^1].Category);
    }

    [Fact]
    public void GroupSaved_AtTheCap_WritesNoTruncatedRow()
    {
        HandlerHarness harness = Harness();

        harness.Publish(WithEntities(new WorkflowGroupSavedNotification(Group(), Messages), Enumerable.Range(0, RecorderDetails.MaxRowsPerNotification).Select(i => Group("G" + i)).ToArray()));

        Assert.Equal(RecorderDetails.MaxRowsPerNotification, harness.Written.Count);
        Assert.All(harness.Written, r => Assert.Null(r.Details));
    }

#if UMBRACO_18
    [Fact]
    public void ConfigSaved_IsExactlyOneRow()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowConfigSavedNotification(Substitute.For<IWorkflowConfig>(), Messages));

        RecorderEntry row = Assert.Single(harness.Written);
        Assert.Equal(nameof(WorkflowConfigSavedNotification), row.RawType);
        Assert.Equal("workflowConfig", row.EntityType);
    }
#endif

    [Fact]
    public void NoBackOfficeUser_StoresNullUserKey()
    {
        HandlerHarness harness = Harness(withUser: false);

        harness.Publish(new WorkflowInstanceCompletedNotification(Instance(), Messages));
        harness.Publish(new WorkflowGroupDeletedNotification(Group(), Messages));

        Assert.All(harness.Written, row => Assert.Null(row.UserKey));
        Assert.Equal(2, harness.Written.Count);
    }

    [Fact]
    public void ThrowingWriter_NeverThrowsIntoWorkflowsSave()
    {
        HandlerHarness harness = Harness(writerFailure: new InvalidOperationException(Sentinel));

        Exception? thrown = Record.Exception(() =>
        {
            harness.Publish(new WorkflowSettingsSavedNotification(Substitute.For<ISettings>(), Messages));
            harness.Publish(new WorkflowGroupSavedNotification(Group(), Messages));
            harness.Publish(new WorkflowInstanceCreatedNotification(Instance(), Messages));
        });

        Assert.Null(thrown);
    }

    [Fact]
    public void GroupDetails_NeverCarryTheGroupEmailOrAnyFreeText()
    {
        HandlerHarness harness = Harness();

        harness.Publish(new WorkflowGroupSavedNotification(Group(), Messages));

        RecorderEntry row = Assert.Single(harness.Written);
        Assert.Null(row.Details);
        HandlerHarness.AssertNoTrace(row, Sentinel);
    }
}
