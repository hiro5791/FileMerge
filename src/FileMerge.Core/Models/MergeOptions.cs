using System.Text;

namespace FileMerge.Models;

/// <summary>
/// Everything the merge engine needs, snapshotted before a run starts.
/// <para>
/// Every default here is "change nothing". The output of a default run is the input files
/// concatenated byte for byte, which is the only result that can never corrupt a file whose
/// encoding was detected wrongly. Each transformation below is opt-in.
/// </para>
/// </summary>
public sealed class MergeOptions
{
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>
    /// <see cref="OutputEncodingKind.Preserve"/> keeps every byte. Anything else decodes each
    /// file with its own encoding and re-encodes the result, which is the only way to merge
    /// files that do not share an encoding, and the only way to damage them.
    /// </summary>
    public OutputEncodingKind OutputEncoding { get; init; } = OutputEncodingKind.Preserve;

    /// <summary>Encoding assumed when a file has no BOM and is not valid UTF-8. Null means the system ANSI code page.</summary>
    public Encoding? FallbackEncoding { get; init; }

    /// <summary>Forces every input to be read with this encoding instead of detecting one.</summary>
    public Encoding? ForcedInputEncoding { get; init; }

    public NewlineMode Newline { get; init; } = NewlineMode.Keep;

    public SeparatorMode Separator { get; init; } = SeparatorMode.None;

    /// <summary>Template for <see cref="SeparatorMode.Custom"/>. Supports {name} {path} {index} {ext}.</summary>
    public string SeparatorTemplate { get; init; } = "----- {name} -----";

    /// <summary>Inserts a line break when a file does not end with one, so the next file starts on its own line.</summary>
    public bool EnsureTrailingNewline { get; init; }

    /// <summary>Drops the first line of every file after the first one. For CSV/TSV with repeated headers.</summary>
    public bool SkipRepeatedHeader { get; init; }

    /// <summary>Removes blank lines at the end of each file before appending the next one.</summary>
    public bool TrimTrailingBlankLines { get; init; }

    /// <summary>
    /// Strips the byte order mark from every file after the first. Without it a BOM ends up
    /// in the middle of the output as an invisible U+FEFF; with it, those few bytes are the
    /// only thing removed.
    /// </summary>
    public bool RemoveInnerBoms { get; init; }

    public ExistingFileAction ExistingFile { get; init; } = ExistingFileAction.Ask;

    /// <summary>
    /// True when at least one option asks for the text pipeline. When false the engine copies
    /// bytes, which is faster and cannot alter the content.
    /// </summary>
    public bool RequiresTextPipeline =>
        OutputEncoding != OutputEncodingKind.Preserve ||
        ForcedInputEncoding is not null ||
        Newline != NewlineMode.Keep ||
        Separator != SeparatorMode.None ||
        EnsureTrailingNewline ||
        SkipRepeatedHeader ||
        TrimTrailingBlankLines;

    public MergeMode ResolvedMode => RequiresTextPipeline ? MergeMode.Text : MergeMode.Binary;
}
