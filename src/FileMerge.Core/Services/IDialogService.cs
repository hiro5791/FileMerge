using FileMerge.Models;

namespace FileMerge.Services;

public sealed record FolderPickResult(string Folder, string Filter, bool Recursive, bool IncludeHidden);

/// <summary>
/// Everything the view model needs from the window layer. Keeps file pickers and message boxes
/// out of the view model so its logic stays testable.
/// </summary>
public interface IDialogService
{
    IReadOnlyList<string> PickFiles();

    FolderPickResult? PickFolder(string initialFilter, bool initialRecursive, bool initialIncludeHidden);

    string? PickOutputFile(string suggestedFileName, bool binary);

    bool ConfirmOverwrite(string path);

    void ShowResult(MergeResult result, string summaryText);

    void ShowError(string message);
}
