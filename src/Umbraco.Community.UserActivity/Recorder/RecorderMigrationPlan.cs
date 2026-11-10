using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>
/// Creates the recorder table on install or upgrade (design 4.1). Public because Umbraco finds package migration
/// plans by type scan. The plan name is the NuGet ID, as for any package, and is permanent: it is the key under which
/// Umbraco remembers how far the plan ran.
/// </summary>
public sealed class RecorderMigrationPlan : PackageMigrationPlan
{
    internal const string FirstStep = "9a6d1d3e-5b0c-4f4f-9d0e-2f6a1c1b7e10";

    public RecorderMigrationPlan()
        : base("Umbraco.Community.UserActivity")
    {
    }

    protected override void DefinePlan() => To<CreateRecorderTable>(FirstStep);
}

/// <summary>
/// Idempotent: a half-applied or re-run plan (a restored database, a site that created the table by hand) finds what
/// exists and creates only what is missing. It touches nothing but this table and its two indexes.
/// </summary>
internal sealed class CreateRecorderTable : AsyncMigrationBase
{
    public CreateRecorderTable(IMigrationContext context)
        : base(context)
    {
    }

    protected override Task MigrateAsync()
    {
        if (!TableExists(RecorderTable.Name))
        {
            Create.Table<ActivityEventDto>().Do();
            return Task.CompletedTask;
        }

        if (!IndexExists(RecorderTable.OccurredIndex))
        {
            Create.Index(RecorderTable.OccurredIndex).OnTable(RecorderTable.Name).OnColumn("OccurredUtc").Ascending().WithOptions().NonClustered().Do();
        }

        if (!IndexExists(RecorderTable.UserIndex))
        {
            Create.Index(RecorderTable.UserIndex).OnTable(RecorderTable.Name).OnColumn("UserKey").Ascending().OnColumn("OccurredUtc").Ascending().WithOptions().NonClustered().Do();
        }

        return Task.CompletedTask;
    }
}
