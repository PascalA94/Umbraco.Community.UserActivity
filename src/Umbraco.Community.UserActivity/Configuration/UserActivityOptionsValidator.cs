using Microsoft.Extensions.Options;

namespace Umbraco.Community.UserActivity.Configuration;

/// <summary>
/// Start-up validation (<c>ValidateOnStart</c>). Messages name the key only, never the configured value, so a
/// misconfigured value can't end up in logs.
/// </summary>
public sealed class UserActivityOptionsValidator : IValidateOptions<UserActivityOptions>
{
    /// <summary>The hard ceiling on a date window (design 3.7, 7.3).</summary>
    public const int RangeCeilingDays = 366;

    /// <summary>Export rows are held in memory and each source is asked for MaxRows + 1, so the cap needs a ceiling.</summary>
    public const int ExportRowsCeiling = 100_000;

    private const string Prefix = UserActivityOptions.SectionName + ":";

    public ValidateOptionsResult Validate(string? name, UserActivityOptions options)
    {
        var failures = new List<string>();

        if (options.EffectiveAllowedUserGroups.All(string.IsNullOrWhiteSpace))
        {
            failures.Add(Prefix + "AllowedUserGroups must list at least one user group alias.");
        }

        if (options.MaxRangeDays < 1 || options.MaxRangeDays > RangeCeilingDays)
        {
            failures.Add(Prefix + $"MaxRangeDays must be between 1 and {RangeCeilingDays}.");
        }

        if (options.DefaultRangeDays < 1 || options.DefaultRangeDays > options.MaxRangeDays)
        {
            failures.Add(Prefix + "DefaultRangeDays must be between 1 and MaxRangeDays.");
        }

        if (options.MaxDepth < 1)
        {
            failures.Add(Prefix + "MaxDepth must be greater than 0.");
        }

        if (options.SourceTimeoutSeconds < 1)
        {
            failures.Add(Prefix + "SourceTimeoutSeconds must be greater than 0.");
        }

        if (options.Export is null || options.Export.MaxRows < 1 || options.Export.MaxRows > ExportRowsCeiling)
        {
            failures.Add(Prefix + $"Export:MaxRows must be between 1 and {ExportRowsCeiling}.");
        }

        if (options.Recorder is null || options.Recorder.RetentionDays < 0)
        {
            failures.Add(Prefix + "Recorder:RetentionDays must be 0 (keep forever) or a number of days.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
