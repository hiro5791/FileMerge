using System.Windows;
using FileMerge.Localization;

namespace FileMerge.Views;

/// <summary>
/// Replaces MessageBox so the buttons follow the language picked inside the app rather than
/// the Windows display language.
/// </summary>
public partial class MessageWindow : Window
{
    private Action? _tertiaryAction;

    public MessageWindow()
    {
        InitializeComponent();
    }

    public static bool Confirm(Window owner, string headline, string message)
    {
        var window = new MessageWindow
        {
            Owner = owner,
            Title = Loc.Current["App.Title"],
        };

        window.HeadlineText.Text = headline;
        window.BodyText.Text = message;
        window.PrimaryButton.Content = Loc.Current["Common.Yes"];
        window.SecondaryButton.Content = Loc.Current["Common.No"];
        window.SecondaryButton.Visibility = Visibility.Visible;

        return window.ShowDialog() == true;
    }

    public static void Inform(
        Window owner,
        string headline,
        string message,
        string? detail = null,
        string? extraButton = null,
        Action? extraAction = null)
    {
        var window = new MessageWindow
        {
            Owner = owner,
            Title = Loc.Current["App.Title"],
        };

        window.HeadlineText.Text = headline;
        window.BodyText.Text = message;
        window.PrimaryButton.Content = Loc.Current["Common.Ok"];

        if (!string.IsNullOrWhiteSpace(detail))
        {
            window.DetailText.Text = detail;
            window.DetailBox.Visibility = Visibility.Visible;
        }

        if (extraButton is not null && extraAction is not null)
        {
            window.TertiaryButton.Content = extraButton;
            window.TertiaryButton.Visibility = Visibility.Visible;
            window._tertiaryAction = extraAction;
        }

        window.ShowDialog();
    }

    private void OnPrimary(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnSecondary(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnTertiary(object sender, RoutedEventArgs e) => _tertiaryAction?.Invoke();
}
