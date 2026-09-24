using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileMerge.Localization;
using FileMerge.Models;
using FileMerge.Services;

namespace FileMerge.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogs;
    private readonly SettingsService _settingsService;
    private readonly MergeEngine _engine = new();
    private readonly AppSettings _settings;

    private CancellationTokenSource? _mergeCts;
    private CancellationTokenSource? _probeCts;

    /// <summary>
    /// Suppresses saving while the constructor assigns the stored values. Without it the
    /// first property to change would write the settings back before the rest had been read,
    /// overwriting them with defaults.
    /// </summary>
    private bool _initializing = true;

    public MainViewModel(IDialogService dialogs, SettingsService settingsService)
    {
        _dialogs = dialogs;
        _settingsService = settingsService;
        _settings = settingsService.Load();

        Files.CollectionChanged += OnFilesChanged;

        SelectedConflict = Conflicts.FirstOrDefault(o => o.Value == _settings.ExistingFile) ?? Conflicts[0];

        _ensureTrailingNewline = _settings.EnsureTrailingNewline;
        _removeInnerBoms = _settings.RemoveInnerBoms;

        SelectedTheme = Themes.FirstOrDefault(t => t.Value == ThemeManager.Parse(_settings.Theme)) ?? Themes[0];

        SelectedLanguage = Loc.Resolve(_settings.Language ?? Loc.DetectSystemLanguage().Code);
        Loc.Current.SetLanguage(SelectedLanguage.Code);
        Loc.Current.PropertyChanged += (_, _) => RefreshLocalizedText();

        _statusText = Loc.Current["Status.Ready"];

        _initializing = false;
    }

    // ---------------------------------------------------------------- Lists

    public ObservableCollection<FileEntry> Files { get; } = new();

    public IReadOnlyList<LanguageInfo> Languages => Loc.Languages;

    public List<EnumOption<ExistingFileAction>> Conflicts { get; } = new()
    {
        new(ExistingFileAction.Ask, "Output.Conflict.Ask"),
        new(ExistingFileAction.Overwrite, "Output.Conflict.Overwrite"),
        new(ExistingFileAction.AutoRename, "Output.Conflict.AutoRename"),
    };

    public List<EnumOption<AppTheme>> Themes { get; } = new()
    {
        new(AppTheme.System, "Settings.Theme.System"),
        new(AppTheme.Light, "Settings.Theme.Light"),
        new(AppTheme.Dark, "Settings.Theme.Dark"),
    };

    // ---------------------------------------------------------------- State

    [ObservableProperty]
    private LanguageInfo _selectedLanguage = Loc.Languages[0];

    [ObservableProperty]
    private EnumOption<AppTheme>? _selectedTheme;

    [ObservableProperty]
    private EnumOption<ExistingFileAction>? _selectedConflict;

    [ObservableProperty]
    private bool _ensureTrailingNewline;

    [ObservableProperty]
    private bool _removeInnerBoms;

    [ObservableProperty]
    private string _outputPath = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>True when every file looks like text. Only picks the default output extension.</summary>
    [ObservableProperty]
    private bool _looksLikeText;

    // The window deliberately carries no warnings. Joining files without converting anything
    // is the faithful result the user asked for, whether that leaves mixed encodings, a byte
    // order mark mid-file, or two files on the same line. The list shows each file's encoding
    // as information, and the options below are there for anyone who wants a different result.

    public bool IsIdle => !IsBusy;

    public bool HasFiles => Files.Count > 0;

    /// <summary>
    /// The caption. It is the only place the name and version appear, so it has to follow the
    /// language picker rather than being set once at startup.
    /// </summary>
    public string WindowTitle => $"{Loc.Current["App.Title"]} {AppInfo.DisplayVersion}".TrimEnd();

    // ---------------------------------------------------------------- Commands

    [RelayCommand]
    private void AddFiles() => AddPaths(_dialogs.PickFiles(CurrentFolder));

    [RelayCommand]
    private void AddFolder()
    {
        var pick = _dialogs.PickFolder(
            CurrentFolder ?? string.Empty,
            _settings.FolderFilter,
            _settings.FolderRecursive,
            _settings.FolderIncludeHidden);

        if (pick is null)
        {
            return;
        }

        _settings.FolderFilter = pick.Filter;
        _settings.FolderRecursive = pick.Recursive;
        _settings.FolderIncludeHidden = pick.IncludeHidden;
        _settings.LastFolder = pick.Folder;

        AddPaths(FolderScanner.Scan(pick.Folder, new FolderScanOptions
        {
            Filter = pick.Filter,
            Recursive = pick.Recursive,
            IncludeHidden = pick.IncludeHidden,
        }));
    }

    /// <summary>
    /// Where a picker should open: the folder of the files already in the list, else wherever
    /// the user was last working, else nowhere in particular and Windows decides.
    /// </summary>
    private string? CurrentFolder
    {
        get
        {
            string? fromList = Files.LastOrDefault()?.DirectoryName;
            if (!string.IsNullOrEmpty(fromList) && Directory.Exists(fromList))
            {
                return fromList;
            }

            return string.IsNullOrEmpty(_settings.LastFolder) ? null : _settings.LastFolder;
        }
    }

    /// <summary>Adds dropped or picked paths, expanding any folders and skipping duplicates.</summary>
    public void AddPaths(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(Files.Select(f => f.FullPath), StringComparer.OrdinalIgnoreCase);
        var added = new List<FileEntry>();

        foreach (string path in paths)
        {
            IEnumerable<string> expanded;

            if (Directory.Exists(path))
            {
                expanded = FolderScanner.Scan(path, new FolderScanOptions
                {
                    Filter = _settings.FolderFilter,
                    Recursive = _settings.FolderRecursive,
                    IncludeHidden = _settings.FolderIncludeHidden,
                });
            }
            else if (File.Exists(path))
            {
                expanded = new[] { path };
            }
            else
            {
                continue;
            }

            foreach (string file in expanded)
            {
                string full = Path.GetFullPath(file);
                if (existing.Add(full))
                {
                    added.Add(FileEntry.FromPath(full));
                }
            }
        }

        foreach (var entry in added)
        {
            Files.Add(entry);
        }

        if (added.Count > 0)
        {
            _settings.LastFolder = added[^1].DirectoryName;
            SuggestOutputPath();
        }
    }

    [RelayCommand]
    private void Remove(IList? selected)
    {
        if (selected is null)
        {
            return;
        }

        foreach (var item in selected.Cast<FileEntry>().ToList())
        {
            Files.Remove(item);
        }
    }

    [RelayCommand]
    private void ClearAll() => Files.Clear();

    [RelayCommand]
    private void MoveUp(IList? selected) => Move(selected, -1);

    [RelayCommand]
    private void MoveDown(IList? selected) => Move(selected, 1);

    private void Move(IList? selected, int delta)
    {
        if (selected is null || selected.Count == 0)
        {
            return;
        }

        var indexes = selected.Cast<FileEntry>()
            .Select(item => Files.IndexOf(item))
            .Where(i => i >= 0)
            .OrderBy(i => i)
            .ToList();

        if (delta > 0)
        {
            indexes.Reverse();
        }

        foreach (int index in indexes)
        {
            int target = index + delta;
            if (target < 0 || target >= Files.Count)
            {
                return; // The block is already against the edge; moving part of it would reorder it.
            }

            Files.Move(index, target);
        }
    }

    /// <summary>Reorders by drag and drop: moves the dragged row to the drop position.</summary>
    public void MoveItem(FileEntry item, int targetIndex)
    {
        int current = Files.IndexOf(item);
        if (current < 0 || targetIndex < 0 || targetIndex >= Files.Count || current == targetIndex)
        {
            return;
        }

        Files.Move(current, targetIndex);
    }

    [RelayCommand]
    private void Sort(string kind)
    {
        var sorted = Files.ToList();

        switch (kind)
        {
            case "Name":
                sorted.Sort((a, b) => NaturalComparer.Instance.Compare(a.FileName, b.FileName));
                break;
            case "Date":
                sorted.Sort((a, b) => a.Modified.CompareTo(b.Modified));
                break;
            case "Size":
                sorted.Sort((a, b) => a.Size.CompareTo(b.Size));
                break;
            case "Path":
                sorted.Sort((a, b) => NaturalComparer.Instance.Compare(a.FullPath, b.FullPath));
                break;
            case "Reverse":
                sorted.Reverse();
                break;
            default:
                return;
        }

        Reset(sorted);
    }

    private void Reset(List<FileEntry> ordered)
    {
        Files.CollectionChanged -= OnFilesChanged;
        Files.Clear();
        foreach (var entry in ordered)
        {
            Files.Add(entry);
        }

        Files.CollectionChanged += OnFilesChanged;
        OnFilesChanged(Files, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    [RelayCommand]
    private void Browse()
    {
        string suggested = string.IsNullOrWhiteSpace(OutputPath)
            ? SuggestFileName()
            : Path.GetFileName(OutputPath);

        // Open where the output already points, if it points anywhere real.
        string? directory = string.IsNullOrWhiteSpace(OutputPath)
            ? CurrentFolder
            : Path.GetDirectoryName(Path.GetFullPath(OutputPath));

        string? picked = _dialogs.PickOutputFile(directory, suggested);
        if (picked is not null)
        {
            OutputPath = picked;
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            return;
        }

        try
        {
            string full = Path.GetFullPath(OutputPath);
            string arguments = File.Exists(full) ? $"/select,\"{full}\"" : $"\"{Path.GetDirectoryName(full)}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void OpenDonatePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(AppInfo.DonateUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    [RelayCommand]
    private void Cancel() => _mergeCts?.Cancel();

    [RelayCommand]
    private async Task MergeAsync()
    {
        if (Files.Count == 0)
        {
            _dialogs.ShowError(Loc.Current["Error.NoInput"]);
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            Browse();
            if (string.IsNullOrWhiteSpace(OutputPath))
            {
                return;
            }
        }

        string target = Path.GetFullPath(OutputPath);

        if (File.Exists(target))
        {
            switch (SelectedConflict?.Value ?? ExistingFileAction.Ask)
            {
                case ExistingFileAction.Ask when !_dialogs.ConfirmOverwrite(target):
                    return;
                case ExistingFileAction.AutoRename:
                    target = MakeUniquePath(target);
                    OutputPath = target;
                    break;
            }
        }

        var options = BuildOptions(target);
        var inputs = Files.Select(f => f.FullPath).ToList();

        _mergeCts = new CancellationTokenSource();
        IsBusy = true;
        Progress = 0;

        var progress = new Progress<MergeProgress>(p =>
        {
            Progress = p.Fraction * 100;
            StatusText = Loc.Current.Format("Status.Merging", p.FileIndex, p.FileCount, p.CurrentFile);
        });

        try
        {
            var result = await _engine.MergeAsync(inputs, options, progress, _mergeCts.Token);

            if (result.Succeeded)
            {
                StatusText = Loc.Current["Status.Done"];
                Progress = 100;

                string summary = Loc.Current.Format(
                    "Result.Summary",
                    result.FileCount,
                    Path.GetFileName(result.OutputPath),
                    FormatDuration(result.Elapsed));

                _dialogs.ShowResult(result, summary);
            }
            else
            {
                StatusText = Loc.Current["Status.Failed"];
                _dialogs.ShowError(DescribeFailure(result));
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = Loc.Current["Status.Cancelled"];
            Progress = 0;
        }
        finally
        {
            IsBusy = false;
            _mergeCts?.Dispose();
            _mergeCts = null;
            SaveSettings();
        }
    }

    private MergeOptions BuildOptions(string target) => new()
    {
        OutputPath = target,
        EnsureTrailingNewline = EnsureTrailingNewline,
        RemoveInnerBoms = RemoveInnerBoms,
        ExistingFile = SelectedConflict?.Value ?? ExistingFileAction.Ask,
    };

    private static string DescribeFailure(MergeResult result)
    {
        string first = result.Warnings.FirstOrDefault() ?? string.Empty;

        return first switch
        {
            "no-input" => Loc.Current["Error.NoInput"],
            "no-output" => Loc.Current["Error.NoOutput"],
            "output-is-input" => Loc.Current["Error.OutputIsInput"],
            _ => string.Join(Environment.NewLine, result.Warnings),
        };
    }

    // ---------------------------------------------------------------- Inspection

    private void OnFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasFiles));
        UpdateSummary();
        _ = ProbeFilesAsync();
    }

    /// <summary>
    /// Reads the head of each file off the UI thread to fill in the Encoding column.
    /// Nothing here changes a file.
    /// </summary>
    private async Task ProbeFilesAsync()
    {
        _probeCts?.Cancel();
        var cts = new CancellationTokenSource();
        _probeCts = cts;

        var pending = Files.Where(f => f.EncodingLabel is null && f.Error is null).ToList();

        foreach (var entry in pending)
        {
            if (cts.IsCancellationRequested)
            {
                return;
            }

            string path = entry.FullPath;
            try
            {
                var probe = await Task.Run(() => FileProbe.Probe(path, null), cts.Token);
                entry.EncodingLabel = probe.EncodingLabel;
                entry.IsTextLike = probe.IsTextLike;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                entry.Error = ex.Message;
            }
        }

        if (!cts.IsCancellationRequested)
        {
            RefreshDetection();
        }
    }

    /// <summary>
    /// Notes whether the list is all text, which decides the default output extension. It no
    /// longer gates the options: those stay available whatever the files are.
    /// </summary>
    private void RefreshDetection()
    {
        bool splitArchive = Files.Count > 1 && Files.All(f => ContentSniffer.LooksLikeSplitPart(f.FullPath));
        LooksLikeText = Files.Count > 0 && Files.All(f => f.IsTextLike) && !splitArchive;
    }

    // ---------------------------------------------------------------- Settings and text

    partial void OnSelectedLanguageChanged(LanguageInfo value)
    {
        Loc.Current.SetLanguage(value.Code);
        _settings.Language = value.Code;
        SaveSettings();
    }

    partial void OnSelectedThemeChanged(EnumOption<AppTheme>? value)
    {
        if (value is null)
        {
            return;
        }

        ThemeManager.Apply(value.Value);
        _settings.Theme = value.Value.ToString();
        SaveSettings();
    }

    private void RefreshLocalizedText()
    {
        UpdateSummary();
        RefreshDetection();

        if (!IsBusy)
        {
            StatusText = Loc.Current["Status.Ready"];
        }

        // The combo boxes hold option objects whose Display reads from the string table,
        // so they have to be told the text underneath them changed.
        OnPropertyChanged(nameof(Conflicts));
        OnPropertyChanged(nameof(Themes));
        OnPropertyChanged(nameof(WindowTitle));
    }

    private void UpdateSummary() =>
        SummaryText = Files.Count == 0
            ? string.Empty
            : Loc.Current.Format("List.Summary", Files.Count, FormatSize(Files.Sum(f => f.Size)));

    private void SuggestOutputPath()
    {
        if (!string.IsNullOrWhiteSpace(OutputPath) || Files.Count == 0)
        {
            return;
        }

        OutputPath = Path.Combine(Files[0].DirectoryName, SuggestFileName());
    }

    private string SuggestFileName()
    {
        if (Files.Count == 0)
        {
            return "merged.txt";
        }

        string first = Files[0].FileName;

        // A split archive rebuilds into the name without the .001 tail.
        if (ContentSniffer.LooksLikeSplitPart(first))
        {
            string trimmed = Path.GetFileNameWithoutExtension(first);
            return string.IsNullOrWhiteSpace(trimmed) ? "merged.bin" : trimmed;
        }

        string extension = Path.GetExtension(first);
        if (string.IsNullOrEmpty(extension))
        {
            extension = LooksLikeText ? ".txt" : ".bin";
        }

        return "merged" + extension;
    }

    private static string MakeUniquePath(string path)
    {
        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);

        for (int i = 2; i < 1000; i++)
        {
            string candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem} ({Guid.NewGuid():N}){extension}");
    }

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[0]}" : $"{value:0.##} {units[unit]}";
    }

    private static string FormatDuration(TimeSpan span) =>
        span.TotalSeconds < 1
            ? $"{span.TotalMilliseconds:0} ms"
            : span.TotalSeconds < 60
                ? $"{span.TotalSeconds:0.0} s"
                : $"{(int)span.TotalMinutes} min {span.Seconds} s";

    public void SaveSettings()
    {
        if (_initializing)
        {
            return;
        }

        _settings.EnsureTrailingNewline = EnsureTrailingNewline;
        _settings.RemoveInnerBoms = RemoveInnerBoms;
        _settings.ExistingFile = SelectedConflict?.Value ?? ExistingFileAction.Ask;
        _settings.Language = SelectedLanguage.Code;

        _settingsService.Save(_settings);
    }
}
