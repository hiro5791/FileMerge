using System.IO;
using System.Text;
using FileMerge.Services;
using Xunit;

namespace FileMerge.Tests;

public sealed class NaturalComparerTests
{
    [Theory]
    [InlineData("part2.txt", "part10.txt")]
    [InlineData("file.9", "file.10")]
    [InlineData("a.001", "a.002")]
    [InlineData("img2", "img12")]
    public void Numbers_sort_by_value_not_by_digit(string smaller, string larger)
    {
        Assert.True(NaturalComparer.Instance.Compare(smaller, larger) < 0);
        Assert.True(NaturalComparer.Instance.Compare(larger, smaller) > 0);
    }

    [Fact]
    public void Ordinal_sorting_would_get_split_parts_wrong()
    {
        var names = new[] { "a.10", "a.2", "a.1" };

        var natural = names.OrderBy(n => n, NaturalComparer.Instance).ToArray();

        Assert.Equal(new[] { "a.1", "a.2", "a.10" }, natural);
    }
}

public sealed class WildcardTests
{
    [Theory]
    [InlineData("report.log", "*.log", true)]
    [InlineData("report.log", "*.txt", false)]
    [InlineData("report.log", "report.*", true)]
    [InlineData("a1.txt", "a?.txt", true)]
    [InlineData("a12.txt", "a?.txt", false)]
    [InlineData("ANYTHING", "*", true)]
    public void Patterns_match_case_insensitively(string name, string pattern, bool expected) =>
        Assert.Equal(expected, FolderScanner.WildcardMatch(name, pattern));
}

public sealed class EncodingDetectorTests
{
    [Fact]
    public void A_utf8_bom_is_recognised_with_certainty()
    {
        byte[] bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("hi")).ToArray();

        var detected = EncodingDetector.Detect(new MemoryStream(bytes));

        Assert.Equal(65001, detected.Encoding.CodePage);
        Assert.Equal(3, detected.BomLength);
        Assert.Equal(Confidence.Certain, detected.Confidence);
    }

    [Fact]
    public void Bomless_utf8_is_recognised_from_its_byte_pattern()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("日本語のテキスト");

        var detected = EncodingDetector.Detect(new MemoryStream(bytes));

        Assert.Equal(65001, detected.Encoding.CodePage);
        Assert.Equal(0, detected.BomLength);
    }

    [Fact]
    public void Bomless_utf16_is_recognised_from_its_null_bytes()
    {
        byte[] bytes = new UnicodeEncoding(bigEndian: false, byteOrderMark: false)
            .GetBytes("plain ascii text, long enough to sample");

        var detected = EncodingDetector.Detect(new MemoryStream(bytes));

        Assert.Equal(1200, detected.Encoding.CodePage);
    }

    [Fact]
    public void Text_that_is_not_utf8_falls_back_to_the_supplied_encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var sjis = Encoding.GetEncoding(932);

        var detected = EncodingDetector.Detect(new MemoryStream(sjis.GetBytes("日本語")), sjis);

        Assert.Equal(932, detected.Encoding.CodePage);
        Assert.Equal(Confidence.Fallback, detected.Confidence);
    }
}

public sealed class ContentSnifferTests
{
    private static string WriteTemp(byte[] bytes, string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Theory]
    [InlineData("archive.zip.001", true)]
    [InlineData("archive.part01", true)]
    [InlineData("archive.r00", true)]
    [InlineData("notes.txt", false)]
    [InlineData("report.2024", true)]
    public void Split_part_names_are_recognised(string name, bool expected) =>
        Assert.Equal(expected, ContentSniffer.LooksLikeSplitPart(name));

    [Fact]
    public void Plain_text_is_reported_as_text()
    {
        string path = WriteTemp(Encoding.UTF8.GetBytes("hello\r\nworld\r\n"), ".txt");
        try
        {
            Assert.Equal(ContentKind.Text, ContentSniffer.Sniff(path).Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Bytes_with_stray_nulls_are_reported_as_binary()
    {
        byte[] bytes = { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0xF3, 0x91 };
        string path = WriteTemp(bytes, ".unknown");
        try
        {
            Assert.Equal(ContentKind.Binary, ContentSniffer.Sniff(path).Kind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void One_binary_file_makes_the_whole_set_binary()
    {
        string text = WriteTemp(Encoding.UTF8.GetBytes("hello"), ".txt");
        string binary = WriteTemp(new byte[] { 0x00, 0x01, 0x02, 0xFF }, ".bin");
        try
        {
            var (kind, _) = ContentSniffer.SniffSet(new[] { text, binary });
            Assert.Equal(ContentKind.Binary, kind);
        }
        finally
        {
            File.Delete(text);
            File.Delete(binary);
        }
    }
}
