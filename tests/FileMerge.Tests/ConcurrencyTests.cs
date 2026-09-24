using System.IO;
using System.Text;
using FileMerge.Models;
using FileMerge.Services;
using Xunit;

namespace FileMerge.Tests;

/// <summary>
/// Nothing stops the user running several copies of the app at once, so merges have to be
/// safe when they overlap. These tests pin down what happens when they do.
/// </summary>
public sealed class ConcurrencyTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("filemerge-concurrency").FullName;

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

    [Fact]
    public async Task Merges_to_different_outputs_do_not_interfere()
    {
        const int jobs = 8;

        var inputs = new List<string[]>();
        var expected = new List<byte[]>();

        for (int i = 0; i < jobs; i++)
        {
            byte[] a = Encoding.UTF8.GetBytes($"job{i}-a\r\n");
            byte[] b = Encoding.UTF8.GetBytes($"job{i}-b\r\n");
            inputs.Add(new[] { Write($"j{i}-a.txt", a), Write($"j{i}-b.txt", b) });
            expected.Add(a.Concat(b).ToArray());
        }

        var tasks = inputs.Select((files, i) => new MergeEngine().MergeAsync(
            files,
            new MergeOptions { OutputPath = Path.Combine(_dir, $"out{i}.txt") },
            null,
            CancellationToken.None));

        var results = await Task.WhenAll(tasks);

        for (int i = 0; i < jobs; i++)
        {
            Assert.True(results[i].Succeeded);
            Assert.Equal(expected[i], File.ReadAllBytes(Path.Combine(_dir, $"out{i}.txt")));
        }
    }

    [Fact]
    public async Task The_same_file_can_be_read_by_several_merges_at_once()
    {
        byte[] shared = Encoding.UTF8.GetBytes(new string('x', 200_000));
        string source = Write("shared.txt", shared);

        var tasks = Enumerable.Range(0, 6).Select(i => new MergeEngine().MergeAsync(
            new[] { source, source },
            new MergeOptions { OutputPath = Path.Combine(_dir, $"shared-out{i}.txt") },
            null,
            CancellationToken.None));

        var results = await Task.WhenAll(tasks);

        byte[] expected = shared.Concat(shared).ToArray();
        for (int i = 0; i < results.Length; i++)
        {
            Assert.True(results[i].Succeeded);
            Assert.Equal(expected, File.ReadAllBytes(Path.Combine(_dir, $"shared-out{i}.txt")));
        }
    }

    /// <summary>
    /// The interesting case: two merges aimed at one path. Each builds into its own scratch
    /// file and renames it into place at the end, so both get through and the file left behind
    /// is whichever finished last — a complete result of one merge, never a mixture of the two.
    /// </summary>
    [Fact]
    public async Task Two_merges_racing_for_one_output_both_finish_and_neither_output_is_mixed()
    {
        byte[] a = Encoding.UTF8.GetBytes(new string('a', 400_000));
        byte[] b = Encoding.UTF8.GetBytes(new string('b', 400_000));

        string fileA = Write("a.txt", a);
        string fileB = Write("b.txt", b);
        string output = Path.Combine(_dir, "contested.txt");

        byte[] fromA = a.Concat(a).ToArray();
        byte[] fromB = b.Concat(b).ToArray();

        // Repeat, because a race that only sometimes collides is still a race.
        for (int attempt = 0; attempt < 12; attempt++)
        {
            File.Delete(output);

            var first = new MergeEngine().MergeAsync(
                new[] { fileA, fileA },
                new MergeOptions { OutputPath = output },
                null,
                CancellationToken.None);

            var second = new MergeEngine().MergeAsync(
                new[] { fileB, fileB },
                new MergeOptions { OutputPath = output },
                null,
                CancellationToken.None);

            var results = await Task.WhenAll(first, second);

            // Neither run may be starved by the other.
            Assert.All(results, r => Assert.True(
                r.Succeeded,
                $"attempt {attempt}: a run failed with [{string.Join("; ", r.Warnings)}]"));

            byte[] actual = File.ReadAllBytes(output);
            bool isOneOrTheOther = actual.SequenceEqual(fromA) || actual.SequenceEqual(fromB);

            Assert.True(
                isOneOrTheOther,
                $"attempt {attempt}: output was neither merge in full ({actual.Length} bytes), so the two runs interleaved");

            // Nothing may be left lying beside the output.
            Assert.Empty(Directory.GetFiles(_dir, "*.fmtmp"));
        }
    }

    [Fact]
    public async Task A_locked_output_file_is_reported_rather_than_throwing()
    {
        string source = Write("in.txt", Encoding.UTF8.GetBytes("payload"));
        string output = Path.Combine(_dir, "locked.txt");

        // Something else is holding the destination, so the final rename cannot happen.
        using var hold = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None);

        var result = await new MergeEngine().MergeAsync(
            new[] { source },
            new MergeOptions { OutputPath = output },
            null,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotEmpty(result.Warnings);

        // The scratch file must not survive a failure.
        Assert.Empty(Directory.GetFiles(_dir, "*.fmtmp"));
    }

    /// <summary>A scratch file orphaned by an earlier crash must not block later merges.</summary>
    [Fact]
    public async Task A_leftover_scratch_file_does_not_block_a_new_merge()
    {
        string source = Write("in.txt", Encoding.UTF8.GetBytes("payload"));
        string output = Path.Combine(_dir, "out.txt");

        File.WriteAllText(output + ".fmtmp", "rubbish from a run that died");

        var result = await new MergeEngine().MergeAsync(
            new[] { source },
            new MergeOptions { OutputPath = output },
            null,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("payload", File.ReadAllText(output));
    }
}
