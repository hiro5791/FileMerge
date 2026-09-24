using FileMerge.Models;

namespace FileMerge.Services;

public sealed record FolderPickResult(string Folder, string Filter, bool Recursive, bool IncludeHidden);

/// <summary>
/// Everything the view model needs from the window layer. Keeps file pickers and message boxes
/// out of the view model so its logic stays testable.
/// <para>
/// The pickers take the folder to open in. Windows would otherwise drop the user somewhere
/// unrelated, so the app remembers where they were last working and starts there.
/// </para>
/// </summary>
public interface IDialogService
{
    IReadOnlyList<string> PickFiles(string? initialDirectory);

    FolderPickResult? PickFolder(string initialFolder, string initialFilter, bool initialRecursive, bool initialIncludeHidden);

    string? PickOutputFile(string? initialDirectory, string suggestedFileName);

    bool ConfirmOverwrite(string path);

    void ShowResult(MergeResult result, string summaryText);

    void ShowError(string message);
}
