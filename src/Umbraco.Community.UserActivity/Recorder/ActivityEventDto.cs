using NPoco;
using Umbraco.Cms.Infrastructure.Persistence.DatabaseAnnotations;

namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>One recorded event (design 4.1). Internal like core's DTOs: the table is ours, the contract is <c>ActivityEvent</c>.</summary>
[TableName(RecorderTable.Name)]
[PrimaryKey("Id", AutoIncrement = true)]
[ExplicitColumns]
internal sealed class ActivityEventDto
{
    [Column("Id")]
    [PrimaryKeyColumn(AutoIncrement = true, Name = "PK_" + RecorderTable.Name)]
    public long Id { get; set; }

    [Column("OccurredUtc")]
    [Index(IndexTypes.NonClustered, Name = RecorderTable.OccurredIndex)]
    public DateTime OccurredUtc { get; set; }

    [Column("Source")]
    [Length(RecorderTable.SourceMax)]
    public string Source { get; set; } = string.Empty;

    [Column("Category")]
    [Length(RecorderTable.CategoryMax)]
    public string Category { get; set; } = string.Empty;

    [Column("RawType")]
    [Length(RecorderTable.RawTypeMax)]
    public string RawType { get; set; } = string.Empty;

    [Column("UserKey")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [Index(IndexTypes.NonClustered, ForColumns = "UserKey,OccurredUtc", Name = RecorderTable.UserIndex)]
    public Guid? UserKey { get; set; }

    [Column("UserName")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [Length(RecorderTable.UserNameMax)]
    public string? UserName { get; set; }

    [Column("EntityType")]
    [Length(RecorderTable.EntityTypeMax)]
    public string EntityType { get; set; } = string.Empty;

    [Column("EntityKey")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [Length(RecorderTable.EntityKeyMax)]
    public string? EntityKey { get; set; }

    [Column("EntityName")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [Length(RecorderTable.EntityNameMax)]
    public string? EntityName { get; set; }

    [Column("DetailsJson")]
    [NullSetting(NullSetting = NullSettings.Null)]
    [Length(RecorderTable.DetailsMax)]
    public string? DetailsJson { get; set; }
}
