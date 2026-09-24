using System.IO;

namespace FileMerge.Services;

/// <summary>
/// How a file's bytes are laid out, as far as line breaks are concerned. This is all the
/// CRLF option needs, and it deliberately stops short of decoding anything.
/// <para>
/// In every encoding in common use other than UTF-16 and UTF-32 — UTF-8, Shift_JIS, EUC-JP,
/// GBK, Big5, EUC-KR, the Windows and ISO single-byte pages — the bytes 0x0A (LF) and 0x0D
/// (CR) only ever mean those characters: no multi-byte character uses them as a trailing
/// byte. So a line break can be recognised, and CRLF written, as plain bytes, and a file whose
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
        Crlf = BuildCrlf(width, bigEndian);
    }

    /// <summary>Bytes per code unit: 1 for byte-oriented encodings, 2 for UTF-16, 4 for UTF-32.</summary>
    public int Width { get; }

    public bool BigEndian { get; }

    public int BomLength { get; }

    /// <summary>
    /// CRLF as bytes in this layout: 0D 0A, or 0D 00 0A 00 for UTF-16 LE, and so on. Built from
    /// the two control codes directly, so no code page is involved, not even a guessed one.
    /// </summary>
    public byte[] Crlf { get; }

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

        var detected = EncodingDetector.Detect(
            sample.AsSpan(0, read),
            sampleWasTruncated: read == SampleBytes,
            EncodingDetector.SystemAnsi);

        (int width, bool bigEndian) = detected.Encoding.CodePage switch
        {
            1200 => (2, false),
            1201 => (2, true),
            12000 => (4, false),
            12001 => (4, true),
            _ => (1, false),
        };

        return new TextLayout(width, bigEndian, detected.BomLength);
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

    private static byte[] BuildCrlf(int width, bool bigEndian)
    {
        var bytes = new byte[2 * width];
        int low = bigEndian ? width - 1 : 0;
        bytes[low] = Cr;
        bytes[width + low] = Lf;
        return bytes;
    }
}
