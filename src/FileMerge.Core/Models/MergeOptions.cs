namespace FileMerge.Models;

/// <summary>
/// Everything the merge engine needs, snapshotted before a run starts.
/// <para>
/// Nothing here converts or decodes anything. A default run is the inputs joined byte for
/// byte. The two options touch only the edges of a file: a few bytes left off the front, or a
/// line break added to the end.
/// </para>
/// </summary>
public sealed class MergeOptions
{
    public string OutputPath { get; init; } = string.Empty;

    /// <summary>
    /// Adds a line break to a file that does not end with one, so the next file starts on its
    /// own line. The break matches the file's own style; CRLF when it has none.
    /// </summary>
    public bool EnsureTrailingNewline { get; init; }

    /// <summary>
    /// Strips the byte order mark from every file after the first. Without it a BOM ends up
    /// in the middle of the output as an invisible U+FEFF; with it, those few bytes are the
    /// only thing removed.
    /// </summary>
    public bool RemoveInnerBoms { get; init; }

    public ExistingFileAction ExistingFile { get; init; } = ExistingFileAction.Ask;
}
