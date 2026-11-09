namespace Umbraco.Community.UserActivity.Configuration;

/// <summary>Settings for one source under <c>UserActivity:Sources:&lt;Name&gt;</c>.</summary>
public sealed class SourceOptions
{
    /// <summary>On by default. A source whose package is not installed stays hidden whatever this says.</summary>
    public bool Enabled { get; set; } = true;
}
