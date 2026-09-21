using System.Windows;
using Microsoft.Win32;

namespace FileMerge.Services;

public enum AppTheme
{
    System,
    Light,
    Dark,
}

/// <summary>Swaps the colour dictionary at runtime and follows the Windows setting when asked to.</summary>
public static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private static AppTheme _current = AppTheme.System;

    public static AppTheme Current => _current;

    /// <summary>True when the resolved theme is dark, regardless of how it was chosen.</summary>
    public static bool IsDark { get; private set; }

    /// <summary>Raised after the resources have been swapped, so windows can restyle their chrome.</summary>
    public static event Action<bool>? ThemeChanged;

    public static void Apply(AppTheme theme)
    {
        _current = theme;
        bool dark = theme switch
        {
            AppTheme.Dark => true,
            AppTheme.Light => false,
            _ => IsSystemDark(),
        };

        IsDark = dark;

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        var source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);

        // The colour dictionary is always first; replacing it in place keeps DynamicResource
        // lookups in the control styles pointing at the new brushes.
        var replacement = new ResourceDictionary { Source = source };

        if (dictionaries.Count == 0)
        {
            dictionaries.Add(replacement);
        }
        else
        {
            dictionaries[0] = replacement;
        }

        ThemeChanged?.Invoke(dark);
    }

    /// <summary>Re-applies the theme. Call when Windows reports a settings change.</summary>
    public static void Refresh()
    {
        if (_current == AppTheme.System)
        {
            Apply(AppTheme.System);
        }
    }

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                return value == 0;
            }
        }
        catch
        {
            // Registry access can be blocked by policy; light is the safer default.
        }

        return false;
    }

    public static AppTheme Parse(string? name) =>
        Enum.TryParse<AppTheme>(name, ignoreCase: true, out var theme) ? theme : AppTheme.System;
}
