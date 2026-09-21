using System.Windows;
using System.Windows.Threading;
using FileMerge.Services;
using FileMerge.Views;

namespace FileMerge;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        EncodingDetector.EnsureCodePagesRegistered();

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        ThemeManager.Apply(ThemeManager.Parse(settings.Theme));

        SystemEvents_Register();

        DispatcherUnhandledException += OnUnhandledException;

        var window = new MainWindow(settingsService);

        // Paths passed on the command line, e.g. by dropping files onto the executable
        // or by "Open with", are loaded straight into the list.
        if (e.Args.Length > 0)
        {
            window.LoadPaths(e.Args);
        }

        MainWindow = window;
        window.Show();
    }

    private static void SystemEvents_Register()
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (_, args) =>
        {
            if (args.Category == Microsoft.Win32.UserPreferenceCategory.General)
            {
                Current.Dispatcher.Invoke(ThemeManager.Refresh);
            }
        };
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.Message,
            "FileMerge",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}
