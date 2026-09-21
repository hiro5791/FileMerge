using System.IO;
using System.Text;

namespace FileMerge.Services;

/// <summary>What a single quick pass over a file tells the UI.</summary>
public sealed record ProbeResult(string EncodingLabel, bool HasBom, bool EndsWithNewline, bool IsTextLike);

/// <summary>
/// Reads the head and tail of a file so the window can describe it and warn about the few
/// combinations that produce a broken merge. It never modifies anything.
/// </summary>
public static class FileProbe
{
    public static ProbeResult Probe(string path, Encoding? fallback)
    {
        var sniff = ContentSniffer.Sniff(path, fallback);

        if (sniff.Kind == ContentKind.Binary)
        {
            return new ProbeResult(string.Empty, false, true, false);
        }

        var detected = EncodingDetector.Detect(path, fallback);
        return new ProbeResult(
            detected.Label,
            detected.BomLength > 0,
            EndsWithNewline(path),
            true);
    }

    /// <summary>
    /// Looks at the last few bytes. Checking the final two covers UTF-16 as well, where a
    /// line feed is stored as 0A 00 or 00 0A.
    /// </summary>
    public static bool EndsWithNewline(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.Length == 0)
            {
                return true; // Nothing follows, so nothing can run together.
            }

            int take = (int)Math.Min(2, stream.Length);
            stream.Position = stream.Length - take;

            Span<byte> tail = stackalloc byte[2];
            int read = stream.Read(tail[..take]);

            for (int i = 0; i < read; i++)
            {
                if (tail[i] is 0x0A or 0x0D)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return true; // Unreadable here means the merge will report it; do not warn twice.
        }
    }
}
