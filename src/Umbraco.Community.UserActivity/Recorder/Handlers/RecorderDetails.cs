namespace Umbraco.Community.UserActivity.Recorder.Handlers;

/// <summary>Small helpers every handler builds its allow-listed <c>DetailsJson</c> keys with.</summary>
internal static class RecorderDetails
{
    /// <summary>
    /// Rows one notification may write, so a large import or bulk delete cannot flood the table through a single save.
    /// </summary>
    public const int MaxRowsPerNotification = 100;

    /// <summary>
    /// Maps at most <see cref="MaxRowsPerNotification"/> items and, when more arrived, appends one marker row so the
    /// trail says rows were left out instead of silently looking complete.
    /// </summary>
    public static IEnumerable<RecorderEntry> Capped<T>(IEnumerable<T> items, Func<T, RecorderEntry> map, Func<int, RecorderEntry> marker)
    {
        var kept = 0;
        var dropped = 0;
        foreach (T item in items)
        {
            if (kept < MaxRowsPerNotification)
            {
                kept++;
                yield return map(item);
            }
            else
            {
                dropped++;
            }
        }

        if (dropped > 0)
        {
            yield return marker(dropped);
        }
    }

    /// <summary>The allow-listed <c>truncated</c> detail: how many rows a capped notification left out.</summary>
    public static IReadOnlyDictionary<string, string> Truncated(int dropped)
        => new Dictionary<string, string> { ["truncated"] = dropped.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    public static string Flag(bool value) => value ? "true" : "false";

    /// <summary>
    /// The part of a URL path before any query string or fragment. A redirect's query string can carry tokens or
    /// tracking ids, so no handler ever stores one (design 4.1).
    /// </summary>
    public static string? PathOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var end = value.AsSpan().IndexOfAny('?', '#');
        return end < 0 ? value : value[..end];
    }
}
