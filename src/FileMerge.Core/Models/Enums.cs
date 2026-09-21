namespace FileMerge.Models;

/// <summary>How the selected files are combined.</summary>
public enum MergeMode
{
    /// <summary>Decode each file as text, then write one text file.</summary>
    Text,

    /// <summary>Append raw bytes end to end. Used to restore split archives.</summary>
    Binary,
}

public enum OutputEncodingKind
{
    /// <summary>
    /// The default: no re-encoding at all. Bytes are copied through untouched, so a file
    /// whose encoding was guessed wrong is still returned exactly as it was supplied.
    /// </summary>
    Preserve,

    Utf8,
    Utf8Bom,
    Utf16Le,
    Utf16Be,
    SystemAnsi,
}

public enum NewlineMode
{
    Keep,
    Crlf,
    Lf,
    Cr,
}

public enum SeparatorMode
{
    None,
    BlankLine,
    FileNameHeader,
    Custom,
}

public enum SortKind
{
    NameNatural,
    NameOrdinal,
    DateModified,
    Size,
    FullPath,
}

public enum ExistingFileAction
{
    Ask,
    Overwrite,
    AutoRename,
}

