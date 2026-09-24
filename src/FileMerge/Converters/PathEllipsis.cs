using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FileMerge.Converters;

/// <summary>
/// Shows a full path in a <see cref="TextBlock"/>, dropping folders out of the middle when it
/// does not fit: <c>F:\…\duplicates\2023\report.txt</c>.
/// <para>
/// Plain trimming cuts the end, which is the half that says which file this is — several paths
/// under one long root would all read the same. Keeping the drive and as much of the tail as
/// fits is what makes a list of same-named files tellable apart at a glance.
/// </para>
/// </summary>
public static class PathEllipsis
{
    private const string Gap = "…";

    public static readonly DependencyProperty PathProperty = DependencyProperty.RegisterAttached(
        "Path",
        typeof(string),
        typeof(PathEllipsis),
        new PropertyMetadata(string.Empty, OnPathChanged));

    public static void SetPath(DependencyObject element, string value) => element.SetValue(PathProperty, value);

    public static string GetPath(DependencyObject element) => (string)element.GetValue(PathProperty);

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock block)
        {
            return;
        }

        // Re-shorten whenever the column is dragged wider or narrower.
        block.SizeChanged -= OnSizeChanged;
        block.SizeChanged += OnSizeChanged;

        Update(block);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged && sender is TextBlock block)
        {
            Update(block);
        }
    }

    private static void Update(TextBlock block)
    {
        string path = GetPath(block);
        double available = block.ActualWidth;

        // Before the first layout pass there is nothing to measure against.
        block.Text = available > 1 ? Shorten(block, path, available) : path;
    }

    private static string Shorten(TextBlock block, string path, double available)
    {
        if (string.IsNullOrEmpty(path) || Measure(block, path) <= available)
        {
            return path;
        }

        var segments = path.Split(
            new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 3)
        {
            return path; // Nothing in the middle to drop.
        }

        string root = segments[0];
        char sep = System.IO.Path.DirectorySeparatorChar;

        // Keep as much of the tail as fits, always with the drive in front of it.
        for (int keep = segments.Length - 2; keep >= 1; keep--)
        {
            string candidate = $"{root}{sep}{Gap}{sep}{string.Join(sep, segments[^keep..])}";

            if (Measure(block, candidate) <= available)
            {
                return candidate;
            }
        }

        // Not even the file name fits; let the TextBlock trim what is left.
        return segments[^1];
    }

    private static double Measure(TextBlock block, string text)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            block.FlowDirection,
            new Typeface(block.FontFamily, block.FontStyle, block.FontWeight, block.FontStretch),
            block.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(block).PixelsPerDip);

        return formatted.WidthIncludingTrailingWhitespace;
    }
}
