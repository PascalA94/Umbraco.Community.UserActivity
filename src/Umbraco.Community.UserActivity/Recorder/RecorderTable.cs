namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>
/// The recorder's one table (design 4.1). The name, index names and column widths are permanent once released: a
/// migration owns the schema, so changing one later means a new migration step, never an edit here.
/// </summary>
internal static class RecorderTable
{
    public const string Name = "communityUserActivityEvent";
    public const string OccurredIndex = "IX_communityUserActivityEvent_OccurredUtc";
    public const string UserIndex = "IX_communityUserActivityEvent_UserKey_OccurredUtc";

    public const int SourceMax = 32;
    public const int CategoryMax = 32;
    public const int RawTypeMax = 128;
    public const int UserNameMax = 255;
    public const int EntityTypeMax = 64;
    public const int EntityKeyMax = 255;
    public const int EntityNameMax = 255;
    public const int DetailsMax = 4000;
}
