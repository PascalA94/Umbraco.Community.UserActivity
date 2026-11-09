using System.Text;
using Umbraco.Community.UserActivity.Export;

namespace Umbraco.Community.UserActivity.Tests.Export;

public class CsvWriterTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [InlineData("+1+1", "\"'+1+1\"")]
    [InlineData("-2+3", "\"'-2+3\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    [InlineData("\tcmd", "\"'\tcmd\"")]
    [InlineData("\rcmd", "\"'\rcmd\"")]
    [InlineData("plain", "\"plain\"")]
    [InlineData("a=b", "\"a=b\"")]
    [InlineData(null, "\"\"")]
    public void Csv_FormulaPrefixed(string? value, string expected)
        => Assert.Equal(expected, CsvWriter.Escape(value));

    [Fact]
    public void Csv_QuotesDoubled()
        => Assert.Equal("\"say \"\"hi\"\", ok\"", CsvWriter.Escape("say \"hi\", ok"));

    [Fact]
    public void Csv_Utf8Bom_FixedFilename()
    {
        byte[] bytes = CsvWriter.Write([["Name", "Value"], ["Zoë", "=1"]]);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Equal("\"Name\",\"Value\"\r\n\"Zoë\",\"'=1\"\r\n", Encoding.UTF8.GetString(bytes[3..]));
        Assert.Equal("user-activity-20261109-1405.csv", CsvWriter.FileName(new DateTimeOffset(2026, 11, 9, 14, 5, 59, TimeSpan.Zero)));
        Assert.Equal("user-activity-20261109-1405.csv", CsvWriter.FileName(new DateTimeOffset(2026, 11, 9, 15, 5, 59, TimeSpan.FromHours(1))));
    }
}
