using System.IO;
using System.Text;
using FileMerge.Models;
using FileMerge.Services;
using Xunit;

namespace FileMerge.Tests;

/// <summary>
/// The central promise of the app is that a default run changes nothing. Most of these tests
/// exist to keep that promise honest, byte for byte.
/// </summary>
public sealed class MergeEngineTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("filemerge-tests").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
            // A leftover temp directory must not fail the run.
        }
    }

    private string Write(string name, byte[] bytes)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string Out(string name) => Path.Combine(_dir, name);

    private static async Task<MergeResult> RunAsync(IReadOnlyList<string> inputs, MergeOptions options) =>
        await new MergeEngine().MergeAsync(inputs, options, null, CancellationToken.None);

    // ---------------------------------------------------------------- Byte fidelity

    [Fact]
    public async Task Default_run_is_byte_for_byte_concatenation()
    {
        byte[] a = { 0x00, 0x01, 0xFF, 0xFE, 0x41 };
        byte[] b = { 0x90, 0x00, 0x7F };

        string output = Out("merged.bin");
        var result = await RunAsync(
            new[] { Write("a.bin", a), Write("b.bin", b) },
            new MergeOptions { OutputPath = output });

        Assert.True(result.Succeeded);
        Assert.Equal(a.Concat(b).ToArray(), File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Default_run_leaves_mixed_encoding_text_untouched()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        byte[] shiftJis = Encoding.GetEncoding(932).GetBytes("日本語\r\n");
        byte[] utf8 = Encoding.UTF8.GetBytes("日本語\r\n");

        string output = Out("merged.txt");
        await RunAsync(
            new[] { Write("sjis.txt", shiftJis), Write("utf8.txt", utf8) },
            new MergeOptions { OutputPath = output });

        // No conversion means no loss: both halves survive exactly as supplied.
        Assert.Equal(shiftJis.Concat(utf8).ToArray(), File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Default_run_keeps_a_split_archive_intact()
    {
        var random = new Random(1234);
        byte[] whole = new byte[8192];
        random.NextBytes(whole);

        string p1 = Write("payload.001", whole[..3000]);
        string p2 = Write("payload.002", whole[3000..6000]);
        string p3 = Write("payload.003", whole[6000..]);

        string output = Out("payload");
        await RunAsync(new[] { p1, p2, p3 }, new MergeOptions { OutputPath = output });

        Assert.Equal(whole, File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Default_run_keeps_every_byte_order_mark()
    {
        byte[] a = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("Hello\r\n")).ToArray();
        byte[] b = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("World\r\n")).ToArray();

        string output = Out("merged.txt");
        await RunAsync(new[] { Write("a.txt", a), Write("b.txt", b) }, new MergeOptions { OutputPath = output });

        // A stray inner BOM is awkward to read, but removing it without being asked would be
        // a silent edit. It is left in place unless RemoveInnerBoms is turned on.
        Assert.Equal(a.Concat(b).ToArray(), File.ReadAllBytes(output));
    }

    // ---------------------------------------------------------------- Opt-in transformations

    [Fact]
    public async Task Removing_inner_boms_drops_only_those_bytes()
    {
        byte[] preamble = Encoding.UTF8.GetPreamble();
        byte[] a = preamble.Concat(Encoding.UTF8.GetBytes("Hello\r\n")).ToArray();
        byte[] b = preamble.Concat(Encoding.UTF8.GetBytes("World\r\n")).ToArray();

        string output = Out("merged.txt");
        await RunAsync(
            new[] { Write("a.txt", a), Write("b.txt", b) },
            new MergeOptions { OutputPath = output, RemoveInnerBoms = true });

        byte[] expected = a.Concat(b.Skip(preamble.Length)).ToArray();
        Assert.Equal(expected, File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Ensure_trailing_newline_adds_a_break_in_the_style_the_files_use()
    {
        string output = Out("merged.txt");
        await RunAsync(
            new[]
            {
                Write("a.txt", Encoding.UTF8.GetBytes("one\nfirst")),
                Write("b.txt", Encoding.UTF8.GetBytes("second")),
            },
            new MergeOptions { OutputPath = output, EnsureTrailingNewline = true });

        // a.txt uses LF, so LF is what gets added, and b.txt, which has none, borrows it.
        Assert.Equal("one\nfirst\nsecond\n", File.ReadAllText(output, Encoding.UTF8));
    }

    [Fact]
    public async Task Ensure_trailing_newline_uses_crlf_when_no_file_has_a_break()
    {
        string output = Out("merged.txt");
        await RunAsync(
            new[]
            {
                Write("a.txt", Encoding.UTF8.GetBytes("first")),
                Write("b.txt", Encoding.UTF8.GetBytes("second")),
            },
            new MergeOptions { OutputPath = output, EnsureTrailingNewline = true });

        Assert.Equal("first\r\nsecond\r\n", File.ReadAllText(output, Encoding.UTF8));
    }

    [Fact]
    public async Task Ensure_trailing_newline_leaves_files_that_already_end_with_one()
    {
        byte[] a = Encoding.UTF8.GetBytes("first\r\n");
        byte[] b = Encoding.UTF8.GetBytes("second\n");
        byte[] empty = Array.Empty<byte>();

        string output = Out("merged.txt");
        await RunAsync(
            new[] { Write("a.txt", a), Write("empty.txt", empty), Write("b.txt", b) },
            new MergeOptions { OutputPath = output, EnsureTrailingNewline = true });

        Assert.Equal(a.Concat(b).ToArray(), File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Without_ensure_trailing_newline_files_run_together()
    {
        string output = Out("merged.txt");
        await RunAsync(
            new[]
            {
                Write("a.txt", Encoding.UTF8.GetBytes("first")),
                Write("b.txt", Encoding.UTF8.GetBytes("second")),
            },
            new MergeOptions { OutputPath = output });

        Assert.Equal("firstsecond", File.ReadAllText(output, Encoding.UTF8));
    }

    // ---------------------------------------------------------------- Nothing is decoded

    /// <summary>
    /// The point of never reading files as text: content that no decoder would accept must come
    /// through with the options on exactly as it went in, plus the one line break asked for.
    /// </summary>
    [Fact]
    public async Task Options_never_alter_bytes_that_no_decoder_would_accept()
    {
        // 0x80-0xFF soup with no 0x00, 0x0A or 0x0D: invalid as UTF-8, and lossy through
        // Shift_JIS or any other code page.
        var random = new Random(42);
        byte[] Soup(int length) => Enumerable.Range(0, length).Select(_ => (byte)random.Next(0x80, 0x100)).ToArray();

        byte[] one = Soup(5000);
        byte[] two = Soup(5000);

        string output = Out("merged.bin");
        await RunAsync(
            new[] { Write("one.dat", one), Write("two.dat", two) },
            new MergeOptions { OutputPath = output, EnsureTrailingNewline = true, RemoveInnerBoms = true });

        byte[] crlf = { 0x0D, 0x0A };
        Assert.Equal(one.Concat(crlf).Concat(two).Concat(crlf).ToArray(), File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Shift_jis_content_survives_the_options_unchanged()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var sjis = Encoding.GetEncoding(932);

        byte[] a = sjis.GetBytes("日本語の本文\r\n二行目");
        byte[] b = sjis.GetBytes("表示されるはずの行");

        string output = Out("merged.txt");
        await RunAsync(
            new[] { Write("a.txt", a), Write("b.txt", b) },
            new MergeOptions { OutputPath = output, EnsureTrailingNewline = true, RemoveInnerBoms = true });

        byte[] expected = sjis.GetBytes("日本語の本文\r\n二行目\r\n表示されるはずの行\r\n");
        Assert.Equal(expected, File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Utf16_gets_its_line_break_as_whole_code_units()
    {
        var utf16 = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        byte[] a = utf16.GetPreamble().Concat(utf16.GetBytes("line\r\n1,a")).ToArray();
        byte[] b = utf16.GetPreamble().Concat(utf16.GetBytes("2,b")).ToArray();

        string output = Out("merged.csv");
        await RunAsync(
            new[] { Write("a.csv", a), Write("b.csv", b) },
            new MergeOptions { OutputPath = output, EnsureTrailingNewline = true, RemoveInnerBoms = true });

        byte[] expected = utf16.GetPreamble().Concat(utf16.GetBytes("line\r\n1,a\r\n2,b\r\n")).ToArray();
        Assert.Equal(expected, File.ReadAllBytes(output));
    }

    [Fact]
    public async Task Utf16_big_endian_line_breaks_are_written_high_byte_first()
    {
        var utf16be = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        byte[] a = utf16be.GetPreamble().Concat(utf16be.GetBytes("one\ntwo")).ToArray();

        string output = Out("merged.txt");
        await RunAsync(
            new[] { Write("a.txt", a) },
            new MergeOptions { OutputPath = output, EnsureTrailingNewline = true });

        byte[] expected = utf16be.GetPreamble().Concat(utf16be.GetBytes("one\ntwo\n")).ToArray();
        Assert.Equal(expected, File.ReadAllBytes(output));
    }

    // ---------------------------------------------------------------- Safety

    [Fact]
    public async Task Writing_over_an_input_file_is_refused()
    {
        string a = Write("a.txt", Encoding.UTF8.GetBytes("keep me"));

        var result = await RunAsync(new[] { a }, new MergeOptions { OutputPath = a });

        Assert.False(result.Succeeded);
        Assert.Contains("output-is-input", result.Warnings);
        Assert.Equal("keep me", File.ReadAllText(a));
    }

    [Fact]
    public async Task An_empty_input_list_is_refused()
    {
        var result = await RunAsync(Array.Empty<string>(), new MergeOptions { OutputPath = Out("x.txt") });

        Assert.False(result.Succeeded);
        Assert.Contains("no-input", result.Warnings);
    }

    [Fact]
    public async Task A_finished_run_leaves_no_temporary_file_behind()
    {
        string output = Out("merged.txt");
        await RunAsync(
            new[] { Write("a.txt", Encoding.UTF8.GetBytes("x")) },
            new MergeOptions { OutputPath = output });

        Assert.Empty(Directory.GetFiles(_dir, "*.fmtmp"));
    }
}
