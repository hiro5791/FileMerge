using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FileMerge.Models;
using FileMerge.Services;
using FileMerge.ViewModels;

namespace FileMerge.Views;

public partial class MainWindow : Window
{
    private const string ReorderFormat = "FileMerge.FileEntry";

    private readonly MainViewModel _viewModel;

    private Point _dragStart;
    private FileEntry? _dragItem;

    public MainWindow(SettingsService settingsService)
    {
        InitializeComponent();

        _viewModel = new MainViewModel(new DialogService(this), settingsService);
        DataContext = _viewModel;

        // The caption bar can only be restyled once the window has a handle.
        SourceInitialized += (_, _) => WindowChrome.ApplyTitleBarTheme(this, ThemeManager.IsDark);
        ThemeManager.ThemeChanged += dark => WindowChrome.ApplyTitleBarTheme(this, dark);
    }

    /// <summary>Entry point for paths passed on the command line.</summary>
    public void LoadPaths(IEnumerable<string> paths) => _viewModel.AddPaths(paths);

    // ---------------------------------------------------------------- Sort menu

    private void OnSortClick(object sender, RoutedEventArgs e) => SortPopup.IsOpen = !SortPopup.IsOpen;

    private void OnSortItemClick(object sender, RoutedEventArgs e) => SortPopup.IsOpen = false;

    // ---------------------------------------------------------------- Drop from Explorer

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _viewModel.AddPaths(paths);
        }

        DropOverlay.Visibility = Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(ReorderFormat))
        {
            e.Effects = DragDropEffects.Move;
            DropOverlay.Visibility = Visibility.Collapsed;
        }
        else if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            DropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnListDragLeave(object sender, DragEventArgs e) =>
        DropOverlay.Visibility = Visibility.Collapsed;

    private void OnListDrop(object sender, DragEventArgs e)
    {
        DropOverlay.Visibility = Visibility.Collapsed;

        if (e.Data.GetData(ReorderFormat) is FileEntry dragged)
        {
            int target = FindDropIndex(e.GetPosition(FileList));
            if (target >= 0)
            {
                _viewModel.MoveItem(dragged, target);
            }

            _dragItem = null;
            e.Handled = true;
            return;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _viewModel.AddPaths(paths);
        }

        e.Handled = true;
    }

    // ---------------------------------------------------------------- Drag to reorder

    private void OnListPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(null);
        _dragItem = (e.OriginalSource as DependencyObject)
            .FindAncestor<ListViewItem>()?.DataContext as FileEntry;
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem is null)
        {
            return;
        }

        Vector moved = e.GetPosition(null) - _dragStart;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject(ReorderFormat, _dragItem);
        DragDrop.DoDragDrop(FileList, data, DragDropEffects.Move);
        _dragItem = null;
    }

    /// <summary>
    /// Index the dragged row should land on. Dropping past the halfway point of a row
    /// inserts after it, which is what makes dragging to the end of the list work.
    /// </summary>
    private int FindDropIndex(Point position)
    {
        for (int i = 0; i < FileList.Items.Count; i++)
        {
            if (FileList.ItemContainerGenerator.ContainerFromIndex(i) is not ListViewItem container)
            {
                continue;
            }

            Point topLeft = container.TranslatePoint(new Point(0, 0), FileList);
            double bottom = topLeft.Y + container.ActualHeight;

            if (position.Y < bottom)
            {
                return position.Y > topLeft.Y + (container.ActualHeight / 2) ? Math.Min(i + 1, FileList.Items.Count - 1) : i;
            }
        }

        return FileList.Items.Count - 1;
    }

    private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e) => _viewModel.SaveSettings();
}

internal static class VisualTreeExtensions
{
    public static T? FindAncestor<T>(this DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
