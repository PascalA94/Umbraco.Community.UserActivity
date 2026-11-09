using System.Globalization;
using System.Text;

namespace Umbraco.Community.UserActivity.Export;

/// <summary>
/// CSV for the export (design 6.6, 7.3). Every cell is quoted with quotes doubled, and a cell that a spreadsheet
/// would read as a formula (starting with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab, CR or LF) gets a leading
/// <c>'</c>: content names are editor-controlled. UTF-8 with BOM so Excel reads non-ASCII names correctly.
/// </summary>
public static class CsvWriter
{
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r', '\n'];

    public static string Escape(string? value)
    {
        value ??= string.Empty;
        if (value.Length > 0 && FormulaStarts.Contains(value[0]))
        {
            value = "'" + value;
        }

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    public static byte[] Write(IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        foreach (IReadOnlyList<string?> row in rows)
        {
            builder.AppendJoin(',', row.Select(Escape)).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(builder.ToString())];
    }

    /// <summary>A fixed pattern, never built from user input: <c>user-activity-{yyyyMMdd-HHmm}.csv</c> in UTC.</summary>
    public static string FileName(DateTimeOffset now)
        => $"user-activity-{now.UtcDateTime.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.csv";
}
