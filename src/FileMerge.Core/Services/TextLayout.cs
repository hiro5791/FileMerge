using System.IO;
using System.Text;

namespace FileMerge.Services;

/// <summary>
/// How a file's bytes are laid out, as far as line breaks are concerned. This is all the
/// trailing-newline option needs, and it deliberately stops short of decoding anything.
/// <para>
/// In every encoding in common use other than UTF-16 and UTF-32 — UTF-8, Shift_JIS, EUC-JP,
/// GBK, Big5, EUC-KR, the Windows and ISO single-byte pages — the bytes 0x0A (LF) and 0x0D
/// (CR) only ever mean those characters: no multi-byte character uses them as a trailing
/// byte. So a line break can be recognised, and written, as plain bytes, and a file whose
/// encoding was guessed wrong is still handled correctly. UTF-16 and UTF-32 are the exception,
/// and there the same check runs over 2- or 4-byte code units instead.
/// </para>
/// </summary>
internal sealed class TextLayout
{
    private const int SampleBytes = 64 * 1024;

    public const int Lf = 0x0A;
    public const int Cr = 0x0D;

    private TextLayout(int width, bool bigEndian, int bomLength)
    {
        Width = width;
        BigEndian = bigEndian;
        BomLength = bomLength;
    }

    /// <summary>Bytes per code unit: 1 for byte-oriented encodings, 2 for UTF-16, 4 for UTF-32.</summary>
    public int Width { get; }

    public bool BigEndian { get; }

    public int BomLength { get; }

    /// <summary>The first line break found in the file, or null if the sample had none.</summary>
    public string? Newline { get; private set; }

    /// <summary>Samples the start of the stream and puts the position back where it was.</summary>
    public static TextLayout Detect(Stream stream)
    {
        var sample = new byte[SampleBytes];
        long origin = stream.Position;

        int read = 0;
        while (read < sample.Length)
        {
            int n = stream.Read(sample, read, sample.Length - read);
            if (n == 0)
            {
                break;
            }

            read += n;
        }

        stream.Position = origin;

        var span = sample.AsSpan(0, read);
        var detected = EncodingDetector.Detect(span, sampleWasTruncated: read == SampleBytes, EncodingDetector.SystemAnsi);

        (int width, bool bigEndian) = detected.Encoding.CodePage switch
        {
            1200 => (2, false),
            1201 => (2, true),
            12000 => (4, false),
            12001 => (4, true),
            _ => (1, false),
        };

        var layout = new TextLayout(width, bigEndian, detected.BomLength);
        layout.Newline = layout.FindNewline(span[Math.Min(detected.BomLength, span.Length)..]);
        return layout;
    }

    public int UnitAt(ReadOnlySpan<byte> bytes, int offset) => Width switch
    {
        1 => bytes[offset],
        2 => BigEndian
            ? (bytes[offset] << 8) | bytes[offset + 1]
            : bytes[offset] | (bytes[offset + 1] << 8),
        _ => BigEndian
            ? (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3]
            : bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24),
    };

    public static bool IsLineBreak(int unit) => unit is Lf or Cr;

    /// <summary>
    /// A line break as bytes in this layout. Built from the two ASCII control codes directly,
    /// so no code page is involved, not even a guessed one.
    /// </summary>
    public byte[] EncodeNewline(string newline)
    {
        var bytes = new byte[newline.Length * Width];
        for (int i = 0; i < newline.Length; i++)
        {
            int at = i * Width;
            int unit = newline[i];
            int low = BigEndian ? at + Width - 1 : at;
            bytes[low] = (byte)unit;
        }

        return bytes;
    }

    private string? FindNewline(ReadOnlySpan<byte> bytes)
    {
        int end = bytes.Length - (bytes.Length % Width);

        for (int i = 0; i < end; i += Width)
        {
            int unit = UnitAt(bytes, i);

            if (unit == Lf)
            {
                return "\n";
            }

            if (unit == Cr)
            {
                return i + Width < end && UnitAt(bytes, i + Width) == Lf ? "\r\n" : "\r";
            }
        }

        return null;
    }
}
