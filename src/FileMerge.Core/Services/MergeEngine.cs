using System.Diagnostics;
using System.IO;
using FileMerge.Models;

namespace FileMerge.Services;

/// <summary>
/// Runs a merge. Nothing is ever decoded: every file is copied as bytes. With the options off
/// the output is the inputs joined byte for byte; the two options only drop a byte order mark
/// from the front of a file or add a line break to its end.
/// </summary>
public sealed class MergeEngine
{
    private const int BufferBytes = 1024 * 1024;

    public async Task<MergeResult> MergeAsync(
        IReadOnlyList<string> inputs,
        MergeOptions options,
        IProgress<MergeProgress>? progress,
        CancellationToken token)
    {
        var warnings = new List<string>();

        if (inputs.Count == 0)
        {
            return MergeResult.Failure(options.OutputPath, "no-input");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            return MergeResult.Failure(options.OutputPath, "no-output");
        }

        string outputFull = Path.GetFullPath(options.OutputPath);

        // Writing into one of the inputs would truncate it before it is read, so this is fatal.
        foreach (string input in inputs)
        {
            if (string.Equals(Path.GetFullPath(input), outputFull, StringComparison.OrdinalIgnoreCase))
            {
                return MergeResult.Failure(options.OutputPath, "output-is-input");
            }
        }

        string? directory = Path.GetDirectoryName(outputFull);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Build into a sibling temp file so a cancelled or failed run never leaves a
        // half-written file where the user expects a complete one. The name carries a fresh
        // id: two runs aimed at one output would otherwise fight over the same scratch file
        // and both lose, and a leftover from an earlier crash would block every later run.
        // It stays beside the output so the rename at the end is a same-volume move.
        string tempPath = $"{outputFull}.{Guid.NewGuid():N}.fmtmp";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            long written = await Task.Run(
                () => Merge(inputs, tempPath, options, progress, warnings, token),
                token).ConfigureAwait(false);

            stopwatch.Stop();

            MoveIntoPlace(tempPath, outputFull);

            return new MergeResult(true, outputFull, inputs.Count, written, stopwatch.Elapsed, warnings);
        }
        catch (OperationCanceledException)
        {
            TryDelete(tempPath);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(tempPath);
            warnings.Add(ex.Message);
            return new MergeResult(false, outputFull, 0, 0, stopwatch.Elapsed, warnings);
        }
    }

    private static long Merge(
        IReadOnlyList<string> inputs,
        string tempPath,
        MergeOptions options,
        IProgress<MergeProgress>? progress,
        List<string> warnings,
        CancellationToken token)
    {
        long totalBytes = MeasureTotal(inputs);
        long done = 0;
        var buffer = new byte[BufferBytes];

        using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferBytes, FileOptions.SequentialScan);

        for (int i = 0; i < inputs.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            string path = inputs[i];
            int fileIndex = i;

            void Report(int read)
            {
                done += read;
                progress?.Report(new MergeProgress(
                    fileIndex + 1,
                    inputs.Count,
                    Path.GetFileName(path),
                    totalBytes > 0 ? Math.Min(1.0, (double)done / totalBytes) : 0));
            }

            try
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BufferBytes, FileOptions.SequentialScan);

                CopyRaw(input, output, buffer, options.RemoveInnerBoms && fileIndex > 0, Report, token);

                if (options.EnsureTrailingNewline)
                {
                    AppendMissingCrlf(input, output);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                warnings.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }

        output.Flush();
        return output.Length;
    }

    /// <summary>
    /// The default path. Copies every byte, optionally skipping the byte order mark of files
    /// after the first. Nothing else is inspected, so split archives come through intact.
    /// </summary>
    private static void CopyRaw(Stream input, Stream output, byte[] buffer, bool skipBom, Action<int> report, CancellationToken token)
    {
        if (skipBom)
        {
            SkipByteOrderMark(input);
        }

        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            output.Write(buffer, 0, read);
            report(read);
        }
    }

    /// <summary>
    /// Adds CRLF after a file whose last character is not a line break. Only the final code
    /// unit of the file is read to decide, and only CRLF itself is written, so the file's
    /// content is never looked at as text. It is always CRLF, whatever the file uses elsewhere.
    /// </summary>
    private static void AppendMissingCrlf(FileStream input, Stream output)
    {
        input.Position = 0;
        var layout = TextLayout.Detect(input);

        long content = input.Length - layout.BomLength;
        if (content <= 0)
        {
            return; // Empty, or nothing but a BOM: there is no line to finish.
        }

        int width = layout.Width;
        if (content >= width && content % width == 0)
        {
            Span<byte> last = stackalloc byte[4];
            input.Position = input.Length - width;
            input.ReadExactly(last[..width]);

            if (TextLayout.IsLineBreak(layout.UnitAt(last, 0)))
            {
                return;
            }
        }

        output.Write(layout.Crlf);
    }

    /// <summary>Advances the stream past a byte order mark if one is present.</summary>
    private static void SkipByteOrderMark(Stream stream)
    {
        Span<byte> head = stackalloc byte[4];
        int read = stream.Read(head);
        stream.Position = MeasureBomLength(head[..read]);
    }

    internal static int MeasureBomLength(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 4 && head[0] == 0xFF && head[1] == 0xFE && head[2] == 0x00 && head[3] == 0x00)
        {
            return 4;
        }

        if (head.Length >= 4 && head[0] == 0x00 && head[1] == 0x00 && head[2] == 0xFE && head[3] == 0xFF)
        {
            return 4;
        }

        if (head.Length >= 3 && head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF)
        {
            return 3;
        }

        if (head.Length >= 2 && ((head[0] == 0xFF && head[1] == 0xFE) || (head[0] == 0xFE && head[1] == 0xFF)))
        {
            return 2;
        }

        return 0;
    }

    /// <summary>
    /// Renames the finished scratch file over the output. Windows refuses this for a moment
    /// when something else holds the destination — a virus scanner that has just opened the
    /// file, Explorer generating a preview, or a second merge replacing the same path — so a
    /// short retry is the difference between working and failing for no lasting reason. A
    /// destination that is genuinely locked still ends up reported, just a quarter of a second
    /// later.
    /// </summary>
    private static void MoveIntoPlace(string tempPath, string outputPath)
    {
        const int attempts = 10;

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                File.Move(tempPath, outputPath, overwrite: true);
                return;
            }
            catch (Exception ex) when (attempt < attempts && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(25);
            }
        }
    }

    private static long MeasureTotal(IReadOnlyList<string> inputs)
    {
        long total = 0;
        foreach (string path in inputs)
        {
            try
            {
                total += new FileInfo(path).Length;
            }
            catch
            {
                // Size only drives the progress bar; an unreadable file is reported by the copy loop.
            }
        }

        return total;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // A leftover .fmtmp is harmless; failing to delete it must not mask the real error.
        }
    }
}
