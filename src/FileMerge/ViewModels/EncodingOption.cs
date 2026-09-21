using System.Text;
using FileMerge.Localization;
using FileMerge.Services;

namespace FileMerge.ViewModels;

/// <summary>One entry in an encoding combo box. <see cref="CodePage"/> 0 means "let the app decide".</summary>
public sealed class EncodingOption
{
    public EncodingOption(int codePage, string? labelKey = null)
    {
        CodePage = codePage;
        LabelKey = labelKey;
    }

    public int CodePage { get; }

    /// <summary>Set for the two synthetic entries (auto-detect, system default) that need translating.</summary>
    public string? LabelKey { get; }

    public string Display => LabelKey is not null
        ? Loc.Current[LabelKey]
        : EncodingDetector.Describe(Encoding.GetEncoding(CodePage), hasBom: false);

    public Encoding? ToEncoding() => CodePage == 0 ? null : Encoding.GetEncoding(CodePage);

    /// <summary>Code pages offered for reading input files, beyond the automatic choice.</summary>
    public static readonly int[] CommonCodePages =
    {
        65001, // UTF-8
        1200,  // UTF-16 LE
        932,   // Shift_JIS
        936,   // GBK
        950,   // Big5
        949,   // EUC-KR
        1252,  // Western European
        1250,  // Central European
        1251,  // Cyrillic
        1253,  // Greek
        1254,  // Turkish
        1255,  // Hebrew
        1256,  // Arabic
        1257,  // Baltic
        1258,  // Vietnamese
        874,   // Thai
        28591, // ISO-8859-1
        20866, // KOI8-R
    };

    public static List<EncodingOption> BuildInputList()
    {
        var list = new List<EncodingOption> { new(0, "Options.InputEncoding.Auto") };
        list.AddRange(CommonCodePages.Select(cp => new EncodingOption(cp)));
        return list;
    }

    public static List<EncodingOption> BuildFallbackList()
    {
        var list = new List<EncodingOption> { new(0, "Options.Encoding.SystemAnsi") };
        list.AddRange(CommonCodePages.Select(cp => new EncodingOption(cp)));
        return list;
    }
}
