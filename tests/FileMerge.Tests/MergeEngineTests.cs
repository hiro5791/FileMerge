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

        // The stray inner BOM is a defect, but removing it without being asked would be a
        // silent edit. The app warns instead; the bytes stay as they were.
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
    public async Task Converting_to_utf8_reconciles_mixed_encodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        string output = Out("merged.txt");
        await RunAsync(
            new[]
            {
                Write("sjis.txt", Encoding.GetEncoding(932).GetBytes("日本語\r\n")),
                Write("utf8.txt", Encoding.UTF8.GetBytes("日本語\r\n")),
            },
            new MergeOptions { OutputPath = output, OutputEncoding = OutputEncodingKind.Utf8 });

        Assert.Equal("日本語\r\n日本語\r\n", File.ReadAllText(output, Encoding.UTF8));
    }

    [Fact]
    public async Task Newline_normalization_rewrites_only_line_endings()
    {
        string output = Out("merged.txt");
        await RunAsync(
            new[]
            {
                Write("a.txt", Encoding.UTF8.GetBytes("one\r\ntwo\r\n")),
                Write("b.txt", Encoding.UTF8.GetBytes("three\nfour\n")),
            },
            new MergeOptions { OutputPath = output, Newline = NewlineMode.Lf });

        Assert.Equal("one\ntwo\nthree\nfour\n", File.ReadAllText(output, Encoding.UTF8));
    }

    [Fact]
    public async Task Skipping_repeated_headers_drops_only_later_first_lines()
    {
        string output = Out("merged.csv");
        await RunAsync(
            new[]
            {
                Write("1.csv", Encoding.UTF8.GetBytes("id,name\r\n1,a\r\n")),
                Write("2.csv", Encoding.UTF8.GetBytes("id,name\r\n2,b\r\n")),
                Write("3.csv", Encoding.UTF8.GetBytes("id,name\r\n3,c\r\n")),
            },
            new MergeOptions { OutputPath = output, SkipRepeatedHeader = true });

        Assert.Equal("id,name\r\n1,a\r\n2,b\r\n3,c\r\n", File.ReadAllText(output, Encoding.UTF8));
    }

    [Fact]
    public async Task Ensure_trailing_newline_joins_files_that_do_not_end_with_one()
    {
        string output = Out("merged.txt");
        await RunAsync(
            new[]
            {
                Write("a.txt", Encoding.UTF8.GetBytes("first")),
                Write("b.txt", Encoding.UTF8.GetBytes("second")),
            },
            new MergeOptions { OutputPath = output, EnsureTrailingNewline = true, Newline = NewlineMode.Lf });

        Assert.Equal("first\nsecond\n", File.ReadAllText(output, Encoding.UTF8));
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

    [Fact]
    public async Task File_name_headers_are_inserted_before_each_file()
    {
        string output = Out("merged.txt");
        await RunAsync(
            new[]
            {
                Write("a.txt", Encoding.UTF8.GetBytes("one\n")),
                Write("b.txt", Encoding.UTF8.GetBytes("two\n")),
            },
            new MergeOptions
            {
                OutputPath = output,
                Separator = SeparatorMode.Custom,
                SeparatorTemplate = "== {name} ==",
                Newline = NewlineMode.Lf,
            });

        Assert.Equal("== a.txt ==\none\n== b.txt ==\ntwo\n", File.ReadAllText(output, Encoding.UTF8));
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
    public async Task A_failed_run_leaves_no_temporary_file_behind()
    {
        string output = Out("merged.txt");
        await RunAsync(
            new[] { Write("a.txt", Encoding.UTF8.GetBytes("x")) },
            new MergeOptions { OutputPath = output });

        Assert.False(File.Exists(output + ".fmtmp"));
    }

    [Fact]
    public void Default_options_do_not_ask_for_the_text_pipeline()
    {
        var options = new MergeOptions { OutputPath = "out.txt" };

        Assert.False(options.RequiresTextPipeline);
        Assert.Equal(MergeMode.Binary, options.ResolvedMode);
    }
}
