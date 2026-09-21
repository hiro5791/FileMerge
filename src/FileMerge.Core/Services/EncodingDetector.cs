using System.IO;
using System.Text;

namespace FileMerge.Services;

/// <summary>
/// Picks an encoding for a text file: BOM first, then a UTF-8 validity check, then a
/// UTF-16 null-byte heuristic, then the fallback supplied by the caller (normally the
/// system ANSI code page).
/// </summary>
public static class EncodingDetector
{
    private const int SampleBytes = 64 * 1024;

    static EncodingDetector()
    {
        // Brings back Shift_JIS (932), GBK (936), Windows-125x and friends,
        // which .NET Core does not register by default.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Forces the static constructor to run. Call once at startup.</summary>
    public static void EnsureCodePagesRegistered()
    {
    }

    public static Encoding SystemAnsi
    {
        get
        {
            try
            {
                int page = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                return Encoding.GetEncoding(page == 0 ? 65001 : page);
            }
            catch
            {
                return Encoding.UTF8;
            }
        }
    }

    public static DetectedEncoding Detect(string path, Encoding? fallback = null)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan);
        return Detect(stream, fallback);
    }

    public static DetectedEncoding Detect(Stream stream, Encoding? fallback = null)
    {
        fallback ??= SystemAnsi;

        var buffer = new byte[SampleBytes];
        long origin = stream.CanSeek ? stream.Position : 0;
        int read = ReadUpTo(stream, buffer);
        if (stream.CanSeek)
        {
            stream.Position = origin;
        }

        return Detect(buffer.AsSpan(0, read), sampleWasTruncated: read == SampleBytes, fallback);
    }

    public static DetectedEncoding Detect(ReadOnlySpan<byte> sample, bool sampleWasTruncated, Encoding fallback)
    {
        if (TryReadBom(sample, out var bomEncoding, out int bomLength))
        {
            return new DetectedEncoding(bomEncoding!, bomLength, Describe(bomEncoding!, hasBom: true), Confidence.Certain);
        }

        if (sample.Length == 0)
        {
            return new DetectedEncoding(new UTF8Encoding(false), 0, "UTF-8", Confidence.Certain);
        }

        if (LooksLikeUtf16(sample, out bool bigEndian))
        {
            var enc = new UnicodeEncoding(bigEndian, byteOrderMark: false);
            return new DetectedEncoding(enc, 0, bigEndian ? "UTF-16 BE" : "UTF-16 LE", Confidence.Likely);
        }

        // A fixed-size sample can end mid-sequence, so a cut-off tail is not treated as malformed.
        if (IsValidUtf8(sample, allowTruncatedTail: sampleWasTruncated))
        {
            return new DetectedEncoding(new UTF8Encoding(false), 0, "UTF-8", Confidence.Likely);
        }

        return new DetectedEncoding(fallback, 0, Describe(fallback, hasBom: false), Confidence.Fallback);
    }

    private static int ReadUpTo(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int n = stream.Read(buffer, total, buffer.Length - total);
            if (n == 0)
            {
                break;
            }

            total += n;
        }

        return total;
    }

    private static bool TryReadBom(ReadOnlySpan<byte> b, out Encoding? encoding, out int length)
    {
        if (b.Length >= 4 && b[0] == 0xFF && b[1] == 0xFE && b[2] == 0x00 && b[3] == 0x00)
        {
            encoding = new UTF32Encoding(bigEndian: false, byteOrderMark: true);
            length = 4;
            return true;
        }

        if (b.Length >= 4 && b[0] == 0x00 && b[1] == 0x00 && b[2] == 0xFE && b[3] == 0xFF)
        {
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            length = 4;
            return true;
        }

        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
        {
            encoding = new UTF8Encoding(true);
            length = 3;
            return true;
        }

        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
        {
            encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
            length = 2;
            return true;
        }

        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
        {
            encoding = new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
            length = 2;
            return true;
        }

        encoding = null;
        length = 0;
        return false;
    }

    /// <summary>
    /// BOM-less UTF-16 shows a strong run of 0x00 bytes on one parity of the byte index.
    /// ASCII and UTF-8 text have none at all, so a low threshold separates them cleanly.
    /// </summary>
    private static bool LooksLikeUtf16(ReadOnlySpan<byte> b, out bool bigEndian)
    {
        bigEndian = false;
        if (b.Length < 16)
        {
            return false;
        }

        int evenZeros = 0, oddZeros = 0, pairs = 0;
        for (int i = 0; i + 1 < b.Length; i += 2)
        {
            pairs++;
            if (b[i] == 0)
            {
                evenZeros++;
            }

            if (b[i + 1] == 0)
            {
                oddZeros++;
            }
        }

        if (pairs == 0)
        {
            return false;
        }

        double even = (double)evenZeros / pairs;
        double odd = (double)oddZeros / pairs;

        if (odd > 0.30 && even < 0.05)
        {
            bigEndian = false; // "A\0" puts the zeros on odd indexes
            return true;
        }

        if (even > 0.30 && odd < 0.05)
        {
            bigEndian = true; // "\0A" puts the zeros on even indexes
            return true;
        }

        return false;
    }

    internal static bool IsValidUtf8(ReadOnlySpan<byte> b, bool allowTruncatedTail)
    {
        int i = 0;

        while (i < b.Length)
        {
            byte c = b[i];

            if (c < 0x80)
            {
                i++;
                continue;
            }

            int extra;
            int min;
            int cp;

            if (c >= 0xC2 && c <= 0xDF)
            {
                extra = 1;
                min = 0x80;
                cp = c & 0x1F;
            }
            else if (c >= 0xE0 && c <= 0xEF)
            {
                extra = 2;
                min = 0x800;
                cp = c & 0x0F;
            }
            else if (c >= 0xF0 && c <= 0xF4)
            {
                extra = 3;
                min = 0x10000;
                cp = c & 0x07;
            }
            else
            {
                return false; // 0x80-0xC1 and 0xF5-0xFF are never valid lead bytes
            }

            if (i + extra >= b.Length)
            {
                // Cut off by the sample window rather than malformed.
                return allowTruncatedTail;
            }

            for (int k = 1; k <= extra; k++)
            {
                byte cc = b[i + k];
                if ((cc & 0xC0) != 0x80)
                {
                    return false;
                }

                cp = (cp << 6) | (cc & 0x3F);
            }

            if (cp < min || cp > 0x10FFFF || (cp >= 0xD800 && cp <= 0xDFFF))
            {
                return false;
            }

            i += extra + 1;
        }

        return true;
    }

    public static string Describe(Encoding encoding, bool hasBom)
    {
        string name = encoding.CodePage switch
        {
            65001 => "UTF-8",
            1200 => "UTF-16 LE",
            1201 => "UTF-16 BE",
            12000 => "UTF-32 LE",
            12001 => "UTF-32 BE",
            932 => "Shift_JIS",
            936 => "GBK",
            949 => "EUC-KR",
            950 => "Big5",
            1251 => "Windows-1251",
            1252 => "Windows-1252",
            1254 => "Windows-1254",
            _ => encoding.WebName.ToUpperInvariant(),
        };

        return hasBom ? name + " (BOM)" : name;
    }
}

public enum Confidence
{
    Certain,
    Likely,
    Fallback,
}

public sealed record DetectedEncoding(Encoding Encoding, int BomLength, string Label, Confidence Confidence);
