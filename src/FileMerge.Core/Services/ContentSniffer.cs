using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FileMerge.Services;

public enum ContentKind
{
    Text,
    Binary,
}

public sealed record SniffResult(ContentKind Kind, string Reason, bool IsSplitPart);

/// <summary>
/// Decides whether a set of files should be concatenated as decoded text or as raw bytes.
/// The two are not interchangeable: byte-concatenating text leaves stray BOMs in the middle
/// of the output, and text-decoding a split archive destroys it, so the wrong guess is
/// expensive and the checks below are deliberately conservative.
/// </summary>
public static partial class ContentSniffer
{
    private const int SampleBytes = 8 * 1024;

    /// <summary>Matches the usual split-archive tails: .001, .part01, .r00, .z01, .7z.001.</summary>
    [GeneratedRegex(@"\.(?:(?<num>\d{2,4})|part\.?(?<pnum>\d{1,4})|r(?<rnum>\d{2,3})|z(?<znum>\d{2,3}))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SplitPartRegex { get; }

    /// <summary>Extensions that are always raw bytes, whatever the first few KB happen to look like.</summary>
    private static readonly HashSet<string> AlwaysBinary = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".7z", ".rar", ".gz", ".bz2", ".xz", ".tar", ".cab", ".iso", ".img", ".bin",
        ".exe", ".dll", ".msi", ".msix", ".appx", ".pdf", ".png", ".jpg", ".jpeg", ".gif",
        ".bmp", ".webp", ".ico", ".mp3", ".mp4", ".avi", ".mkv", ".mov", ".wav", ".flac",
        ".ttf", ".otf", ".woff", ".woff2", ".db", ".sqlite", ".mdb", ".pak", ".dat",
    };

    /// <summary>Extensions that are always decoded text, even if a stray byte looks odd.</summary>
    private static readonly HashSet<string> AlwaysText = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".csv", ".tsv", ".log", ".md", ".json", ".xml", ".html", ".htm", ".css",
        ".js", ".ts", ".cs", ".c", ".h", ".cpp", ".py", ".rb", ".go", ".rs", ".java",
        ".php", ".sql", ".yml", ".yaml", ".ini", ".cfg", ".conf", ".srt", ".vtt", ".sub",
        ".ps1", ".bat", ".cmd", ".sh", ".tex", ".svg", ".gpx", ".vcf", ".ics",
    };

    public static bool LooksLikeSplitPart(string path) =>
        SplitPartRegex.IsMatch(Path.GetFileName(path));

    public static SniffResult Sniff(string path, Encoding? fallback = null)
    {
        string ext = Path.GetExtension(path);
        bool isSplitPart = LooksLikeSplitPart(path);

        // A .001 tail beats every other signal: the part on its own may well be readable
        // text, yet the point of the merge is to rebuild the original bytes.
        if (isSplitPart)
        {
            return new SniffResult(ContentKind.Binary, "split-part-name", true);
        }

        if (AlwaysBinary.Contains(ext))
        {
            return new SniffResult(ContentKind.Binary, "binary-extension", false);
        }

        byte[] sample;
        int read;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
            sample = new byte[SampleBytes];
            read = stream.Read(sample, 0, sample.Length);
        }
        catch
        {
            // Unreadable here means unreadable later too; raw bytes is the safer default.
            return new SniffResult(ContentKind.Binary, "unreadable", false);
        }

        var span = sample.AsSpan(0, Math.Max(read, 0));

        if (AlwaysText.Contains(ext))
        {
            return new SniffResult(ContentKind.Text, "text-extension", false);
        }

        return new SniffResult(SniffBytes(span, read == SampleBytes, fallback), "content", false);
    }

    private static ContentKind SniffBytes(ReadOnlySpan<byte> span, bool truncated, Encoding? fallback)
    {
        if (span.Length == 0)
        {
            return ContentKind.Text;
        }

        // A BOM is proof of text.
        if (span.Length >= 2 &&
            ((span[0] == 0xEF && span[1] == 0xBB) || (span[0] == 0xFF && span[1] == 0xFE) || (span[0] == 0xFE && span[1] == 0xFF)))
        {
            return ContentKind.Text;
        }

        int nulls = 0;
        int controls = 0;
        for (int i = 0; i < span.Length; i++)
        {
            byte b = span[i];
            if (b == 0)
            {
                nulls++;
            }
            else if (b < 0x09 || (b > 0x0D && b < 0x20) || b == 0x7F)
            {
                controls++;
            }
        }

        // UTF-16 text is roughly half NUL bytes, so a NUL alone is not proof of binary;
        // it is only proof when the NULs are not laid out the way UTF-16 lays them out.
        if (nulls > 0)
        {
            int evenZeros = 0, oddZeros = 0, pairs = 0;
            for (int i = 0; i + 1 < span.Length; i += 2)
            {
                pairs++;
                if (span[i] == 0)
                {
                    evenZeros++;
                }

                if (span[i + 1] == 0)
                {
                    oddZeros++;
                }
            }

            bool utf16Shaped = pairs > 8 &&
                ((oddZeros > pairs * 0.30 && evenZeros < pairs * 0.05) ||
                 (evenZeros > pairs * 0.30 && oddZeros < pairs * 0.05));

            if (!utf16Shaped)
            {
                return ContentKind.Binary;
            }

            return ContentKind.Text;
        }

        // More than a few percent of stray control bytes means this is not prose.
        if (controls > span.Length * 0.03)
        {
            return ContentKind.Binary;
        }

        // No NULs and few control bytes. Valid UTF-8 settles it; otherwise fall back to
        // whether the legacy code page can decode it without producing replacement chars.
        if (EncodingDetector.IsValidUtf8(span, allowTruncatedTail: truncated))
        {
            return ContentKind.Text;
        }

        return DecodesCleanly(span, fallback ?? EncodingDetector.SystemAnsi) ? ContentKind.Text : ContentKind.Binary;
    }

    private static bool DecodesCleanly(ReadOnlySpan<byte> span, Encoding encoding)
    {
        try
        {
            var strict = Encoding.GetEncoding(
                encoding.CodePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
            strict.GetString(span);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the mode for a whole list. One binary file poisons the batch: decoding it as
    /// text would corrupt it, while byte-appending a text file merely leaves a stray BOM.
    /// </summary>
    public static (ContentKind Kind, string Reason) SniffSet(IEnumerable<string> paths, Encoding? fallback = null)
    {
        bool any = false;
        foreach (string path in paths)
        {
            any = true;
            var result = Sniff(path, fallback);
            if (result.Kind == ContentKind.Binary)
            {
                return (ContentKind.Binary, result.Reason);
            }
        }

        return any ? (ContentKind.Text, "content") : (ContentKind.Text, "empty");
    }
}
