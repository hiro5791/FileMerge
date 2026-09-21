using System.Text;

namespace FileMerge.Services;

/// <summary>What a single quick pass over a file tells the UI.</summary>
public sealed record ProbeResult(string EncodingLabel, bool IsTextLike);

/// <summary>
/// Reads the head of a file so the window can describe it: what encoding it appears to be in,
/// and whether it is text at all. It never modifies anything, and the result is shown as
/// information, not as advice.
/// </summary>
public static class FileProbe
{
    public static ProbeResult Probe(string path, Encoding? fallback)
    {
        var sniff = ContentSniffer.Sniff(path, fallback);

        if (sniff.Kind == ContentKind.Binary)
        {
            return new ProbeResult(string.Empty, false);
        }

        var detected = EncodingDetector.Detect(path, fallback);
        return new ProbeResult(detected.Label, true);
    }
}
