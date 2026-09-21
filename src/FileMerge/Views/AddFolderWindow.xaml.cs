using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FileMerge.Localization;
using FileMerge.Services;

namespace FileMerge.Views;

public partial class AddFolderWindow : Window
{
    private readonly DispatcherTimer _previewTimer;
    private CancellationTokenSource? _previewCts;

    public AddFolderWindow(string filter, bool recursive, bool includeHidden)
    {
        InitializeComponent();

        FilterBox.Text = filter;
        RecursiveBox.IsChecked = recursive;
        HiddenBox.IsChecked = includeHidden;

        // Counting matches means walking the tree, so it runs after typing settles.
        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            _ = RefreshPreviewAsync();
        };

        Loaded += (_, _) => FolderBox.Focus();
    }

    public FolderPickResult? Result { get; private set; }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Multiselect = false,
        };

        if (Directory.Exists(FolderBox.Text))
        {
            dialog.InitialDirectory = FolderBox.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            FolderBox.Text = dialog.FolderName;
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e) => SchedulePreview();

    private void OnToggleChanged(object sender, RoutedEventArgs e) => SchedulePreview();

    private void SchedulePreview()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private async Task RefreshPreviewAsync()
    {
        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;

        string folder = FolderBox.Text;
        if (!Directory.Exists(folder))
        {
            FoundText.Text = string.Empty;
            return;
        }

        var options = new FolderScanOptions
        {
            Filter = FilterBox.Text,
            Recursive = RecursiveBox.IsChecked == true,
            IncludeHidden = HiddenBox.IsChecked == true,
        };

        try
        {
            int count = await Task.Run(() => FolderScanner.Scan(folder, options, cts.Token).Count, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                FoundText.Text = Loc.Current.Format("Folder.Found", count);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            FoundText.Text = string.Empty;
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        string folder = FolderBox.Text.Trim();
        if (!Directory.Exists(folder))
        {
            MessageWindow.Inform(this, Loc.Current["Folder.Title"], Loc.Current["Folder.Path"]);
            return;
        }

        Result = new FolderPickResult(
            folder,
            string.IsNullOrWhiteSpace(FilterBox.Text) ? "*.*" : FilterBox.Text.Trim(),
            RecursiveBox.IsChecked == true,
            HiddenBox.IsChecked == true);

        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
