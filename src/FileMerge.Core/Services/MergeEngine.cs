using System.Diagnostics;
using System.IO;
using System.Text;
using FileMerge.Models;

namespace FileMerge.Services;

/// <summary>
/// Runs a merge. Which pipeline is used is decided by the options, not by a mode switch:
/// with everything left at its default the engine copies bytes, and it only decodes text
/// when an option asks for something that cannot be done on bytes.
/// </summary>
public sealed class MergeEngine
{
    private const int TextBufferChars = 64 * 1024;
    private const int BinaryBufferBytes = 1024 * 1024;

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
        // half-written file where the user expects a complete one.
        string tempPath = outputFull + ".fmtmp";
        var stopwatch = Stopwatch.StartNew();

        try
        {
            long written = await Task.Run(
                () => options.RequiresTextPipeline
                    ? MergeText(inputs, tempPath, options, progress, warnings, token)
                    : MergeBytes(inputs, tempPath, options, progress, warnings, token),
                token).ConfigureAwait(false);

            stopwatch.Stop();

            File.Move(tempPath, outputFull, overwrite: true);

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

    // ---------------------------------------------------------------- Byte pipeline

    /// <summary>
    /// The default pipeline. Copies every input byte for byte, optionally skipping the byte
    /// order mark of files after the first. Nothing else is inspected or altered, so this is
    /// equally correct for split archives and for text.
    /// </summary>
    private static long MergeBytes(
        IReadOnlyList<string> inputs,
        string tempPath,
        MergeOptions options,
        IProgress<MergeProgress>? progress,
        List<string> warnings,
        CancellationToken token)
    {
        long totalBytes = MeasureTotal(inputs);
        long done = 0;
        var buffer = new byte[BinaryBufferBytes];

        using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, BinaryBufferBytes, FileOptions.SequentialScan);

        for (int i = 0; i < inputs.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            string path = inputs[i];

            try
            {
                using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, BinaryBufferBytes, FileOptions.SequentialScan);

                if (options.RemoveInnerBoms && i > 0)
                {
                    SkipByteOrderMark(input);
                }

                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    token.ThrowIfCancellationRequested();
                    output.Write(buffer, 0, read);
                    done += read;

                    progress?.Report(new MergeProgress(
                        i + 1,
                        inputs.Count,
                        Path.GetFileName(path),
                        totalBytes > 0 ? Math.Min(1.0, (double)done / totalBytes) : 0));
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

    /// <summary>Advances the stream past a byte order mark if one is present.</summary>
    private static void SkipByteOrderMark(Stream stream)
    {
        Span<byte> head = stackalloc byte[4];
        int read = stream.Read(head);
        int bomLength = MeasureBomLength(head[..read]);
        stream.Position = bomLength;
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

    // ---------------------------------------------------------------- Text pipeline

    private static long MergeText(
        IReadOnlyList<string> inputs,
        string tempPath,
        MergeOptions options,
        IProgress<MergeProgress>? progress,
        List<string> warnings,
        CancellationToken token)
    {
        long totalBytes = MeasureTotal(inputs);
        long done = 0;

        string? newline = options.Newline switch
        {
            NewlineMode.Crlf => "\r\n",
            NewlineMode.Lf => "\n",
            NewlineMode.Cr => "\r",
            _ => null,
        };

        using var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan);

        var normalizer = new NormalizingWriter(newline, options.TrimTrailingBlankLines);
        var buffer = new char[TextBufferChars];

        StreamWriter? writer = null;
        int currentCodePage = -1;

        try
        {
            for (int i = 0; i < inputs.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                string path = inputs[i];

                try
                {
                    var detected = ResolveInputEncoding(path, options, warnings);

                    // Preserve writes each file back in the encoding it arrived in, so a wrong
                    // guess costs nothing: the bytes round-trip unchanged.
                    Encoding target = options.OutputEncoding == OutputEncodingKind.Preserve
                        ? detected.Encoding
                        : ResolveOutputEncoding(options.OutputEncoding);

                    // Only the first writer may emit a preamble; a BOM from any later writer
                    // would land in the middle of the output.
                    Encoding writerEncoding = i == 0 ? target : WithoutPreamble(target);

                    if (writer is null || writerEncoding.CodePage != currentCodePage)
                    {
                        normalizer.Flush();
                        writer?.Flush();
                        writer = new StreamWriter(stream, writerEncoding, 64 * 1024, leaveOpen: true);
                        normalizer.Attach(writer);
                        currentCodePage = writerEncoding.CodePage;
                    }

                    WriteSeparator(normalizer, options, path, i);

                    // The header line is only redundant from the second file onward.
                    normalizer.BeginFile(skipFirstLine: options.SkipRepeatedHeader && i > 0);

                    using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, FileOptions.SequentialScan);

                    // detectEncodingFromByteOrderMarks strips this file's BOM so it never
                    // lands in the middle of the merged output.
                    using var reader = new StreamReader(input, detected.Encoding, detectEncodingFromByteOrderMarks: true);

                    int read;
                    while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        normalizer.Write(buffer.AsSpan(0, read));

                        done = Math.Min(totalBytes, done + read);
                        progress?.Report(new MergeProgress(
                            i + 1,
                            inputs.Count,
                            Path.GetFileName(path),
                            totalBytes > 0 ? Math.Min(1.0, (double)done / totalBytes) : 0));
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

                normalizer.EndFile(options.EnsureTrailingNewline);
            }

            normalizer.Flush();
            writer?.Flush();
            stream.Flush();
            return stream.Length;
        }
        finally
        {
            writer?.Dispose();
        }
    }

    private static DetectedEncoding ResolveInputEncoding(string path, MergeOptions options, List<string> warnings)
    {
        if (options.ForcedInputEncoding is not null)
        {
            return new DetectedEncoding(options.ForcedInputEncoding, 0, options.ForcedInputEncoding.WebName, Confidence.Certain);
        }

        var detected = EncodingDetector.Detect(path, options.FallbackEncoding);
        if (detected.Confidence == Confidence.Fallback)
        {
            warnings.Add($"{Path.GetFileName(path)}: encoding-guessed:{detected.Label}");
        }

        return detected;
    }

    private static void WriteSeparator(NormalizingWriter writer, MergeOptions options, string path, int index)
    {
        switch (options.Separator)
        {
            case SeparatorMode.None:
                return;

            case SeparatorMode.BlankLine:
                if (index > 0)
                {
                    if (!writer.AtLineStart)
                    {
                        writer.WriteLineBreak();
                    }

                    writer.WriteLineBreak();
                }

                return;

            case SeparatorMode.FileNameHeader:
            case SeparatorMode.Custom:
                string template = options.Separator == SeparatorMode.FileNameHeader
                    ? "----- {name} -----"
                    : options.SeparatorTemplate;

                if (index > 0 && !writer.AtLineStart)
                {
                    writer.WriteLineBreak();
                }

                writer.WriteLiteral(Expand(template, path, index));
                writer.WriteLineBreak();
                return;
        }
    }

    private static string Expand(string template, string path, int index) =>
        template
            .Replace("{name}", Path.GetFileName(path), StringComparison.OrdinalIgnoreCase)
            .Replace("{path}", path, StringComparison.OrdinalIgnoreCase)
            .Replace("{ext}", Path.GetExtension(path), StringComparison.OrdinalIgnoreCase)
            .Replace("{index}", (index + 1).ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);

    private static Encoding ResolveOutputEncoding(OutputEncodingKind kind) => kind switch
    {
        OutputEncodingKind.Utf8 => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
        OutputEncodingKind.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        OutputEncodingKind.Utf16Le => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
        OutputEncodingKind.Utf16Be => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
        OutputEncodingKind.SystemAnsi => EncodingDetector.SystemAnsi,
        _ => new UTF8Encoding(false),
    };

    /// <summary>Same encoding, minus the preamble, so only the first writer can emit a BOM.</summary>
    private static Encoding WithoutPreamble(Encoding encoding) => encoding.CodePage switch
    {
        65001 => new UTF8Encoding(false),
        1200 => new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
        1201 => new UnicodeEncoding(bigEndian: true, byteOrderMark: false),
        12000 => new UTF32Encoding(bigEndian: false, byteOrderMark: false),
        12001 => new UTF32Encoding(bigEndian: true, byteOrderMark: false),
        _ => encoding,
    };

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
