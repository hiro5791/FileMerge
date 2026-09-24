using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FileMerge.Models;

/// <summary>One row in the merge list. Order in the list is the order written to the output.</summary>
public partial class FileEntry : ObservableObject
{
    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private DateTime _modified;

    /// <summary>Detected text encoding, or null until the file has been probed.</summary>
    [ObservableProperty]
    private string? _encodingLabel;

    /// <summary>Non-null when the file could not be read; shown inline in the list.</summary>
    [ObservableProperty]
    private string? _error;

    /// <summary>True when the content looks like text rather than raw bytes.</summary>
    [ObservableProperty]
    private bool _isTextLike = true;
    public string FileName => Path.GetFileName(FullPath);

    public string DirectoryName => Path.GetDirectoryName(FullPath) ?? string.Empty;

    public string Extension => Path.GetExtension(FullPath);

    public static FileEntry FromPath(string path)
    {
        var info = new FileInfo(path);
        return new FileEntry
        {
            FullPath = info.FullName,
            Size = info.Exists ? info.Length : 0,
            Modified = info.Exists ? info.LastWriteTime : DateTime.MinValue,
            Error = info.Exists ? null : "missing",
        };
    }

    partial void OnFullPathChanged(string value)
    {
        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(DirectoryName));
        OnPropertyChanged(nameof(Extension));
    }
}
