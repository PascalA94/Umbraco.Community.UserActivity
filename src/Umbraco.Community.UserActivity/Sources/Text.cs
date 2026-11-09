namespace Umbraco.Community.UserActivity.Sources;

/// <summary>String caps from the event contract (design 3.4).</summary>
internal static class Text
{
    public static string? Cap(string? value, int max) => value is null || value.Length <= max ? value : value[..max];

    public static void AddCapped(IDictionary<string, string> details, string key, string? value)
    {
        if (value is not null && details.Count < ActivityEvent.MaxDetails)
        {
            details[key] = Cap(value, ActivityEvent.MaxDetailLength)!;
        }
    }

    /// <summary><c>DocumentType</c> to <c>documentType</c>: entity types are API values in lower camel case.</summary>
    public static string? CamelCase(string? value)
        => string.IsNullOrEmpty(value) ? null : char.ToLowerInvariant(value[0]) + value[1..];
}
