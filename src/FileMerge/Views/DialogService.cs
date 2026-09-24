using System.Diagnostics;
using System.IO;
using System.Windows;
using FileMerge.Localization;
using FileMerge.Models;
using FileMerge.Services;

namespace FileMerge.Views;

/// <summary>Window-layer implementation of <see cref="IDialogService"/>.</summary>
public sealed class DialogService : IDialogService
{
    private readonly Window _owner;

    public DialogService(Window owner) => _owner = owner;

    public IReadOnlyList<string> PickFiles(string? initialDirectory)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = Loc.Current["Action.AddFiles"],
            Filter = $"{Loc.Current["Common.Files"]} (*.*)|*.*",
        };

        ApplyInitialDirectory(dialog, initialDirectory);

        return dialog.ShowDialog(_owner) == true ? dialog.FileNames : Array.Empty<string>();
    }

    public FolderPickResult? PickFolder(string initialFolder, string initialFilter, bool initialRecursive, bool initialIncludeHidden)
    {
        var window = new AddFolderWindow(initialFolder, initialFilter, initialRecursive, initialIncludeHidden)
        {
            Owner = _owner,
        };

        return window.ShowDialog() == true ? window.Result : null;
    }

    public string? PickOutputFile(string? initialDirectory, string suggestedFileName)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = Loc.Current["Output.Label"],
            FileName = suggestedFileName,
            Filter = $"{Loc.Current["Common.Files"]} (*.*)|*.*",
            OverwritePrompt = false, // The app asks, using its own language.
        };

        ApplyInitialDirectory(dialog, initialDirectory);

        return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
    }

    /// <summary>
    /// Opens the dialog where the user was last working. A path that no longer exists is
    /// ignored rather than passed on, because the dialog would then pick its own fallback
    /// silently and the setting would look broken.
    /// </summary>
    private static void ApplyInitialDirectory(Microsoft.Win32.FileDialog dialog, string? directory)
    {
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            dialog.InitialDirectory = directory;
        }
    }

    public bool ConfirmOverwrite(string path) =>
        MessageWindow.Confirm(
            _owner,
            Loc.Current["Output.OnConflict"],
            Loc.Current.Format("Output.ConfirmOverwrite", Path.GetFileName(path)));

    public void ShowResult(MergeResult result, string summaryText)
    {
        string? detail = result.Warnings.Count > 0
            ? $"{Loc.Current["Result.Warnings"]}:{Environment.NewLine}{string.Join(Environment.NewLine, result.Warnings)}"
            : null;

        MessageWindow.Inform(
            _owner,
            Loc.Current["Result.Title"],
            summaryText,
            detail,
            Loc.Current["Action.OpenFolder"],
            () => RevealInExplorer(result.OutputPath));
    }

    public void ShowError(string message) =>
        MessageWindow.Inform(_owner, Loc.Current["Status.Failed"], message);

    private static void RevealInExplorer(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // Explorer being unavailable is not worth a second error dialog.
        }
    }
}
