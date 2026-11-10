namespace Umbraco.Community.UserActivity.Recorder;

/// <summary>What the recorder handlers need from <see cref="RecorderWriter"/>, so a test can see what a handler asked to store.</summary>
internal interface IRecorderWriter
{
    /// <summary>Stores one event. Implementations never throw into the caller.</summary>
    void Write(RecorderEntry entry);
}
