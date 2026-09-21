using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace FileMerge.Localization;

public sealed record LanguageInfo(string Code, string NativeName, string EnglishName, bool RightToLeft);

/// <summary>
/// Runtime string table. Every language ships as an embedded JSON resource rather than as a
/// satellite assembly, because satellite assemblies would either break the single-file
/// portable build or scatter extra folders next to the executable.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string FallbackLanguage = "en";

    /// <summary>The twenty languages the app ships with, in the order shown in the picker.</summary>
    public static readonly IReadOnlyList<LanguageInfo> Languages = new[]
    {
        new LanguageInfo("en", "English", "English", false),
        new LanguageInfo("ja", "日本語", "Japanese", false),
        new LanguageInfo("zh-Hans", "简体中文", "Chinese (Simplified)", false),
        new LanguageInfo("zh-Hant", "繁體中文", "Chinese (Traditional)", false),
        new LanguageInfo("ko", "한국어", "Korean", false),
        new LanguageInfo("es", "Español", "Spanish", false),
        new LanguageInfo("pt-BR", "Português (Brasil)", "Portuguese (Brazil)", false),
        new LanguageInfo("fr", "Français", "French", false),
        new LanguageInfo("de", "Deutsch", "German", false),
        new LanguageInfo("it", "Italiano", "Italian", false),
        new LanguageInfo("ru", "Русский", "Russian", false),
        new LanguageInfo("uk", "Українська", "Ukrainian", false),
        new LanguageInfo("pl", "Polski", "Polish", false),
        new LanguageInfo("nl", "Nederlands", "Dutch", false),
        new LanguageInfo("tr", "Türkçe", "Turkish", false),
        new LanguageInfo("ar", "العربية", "Arabic", true),
        new LanguageInfo("hi", "हिन्दी", "Hindi", false),
        new LanguageInfo("id", "Bahasa Indonesia", "Indonesian", false),
        new LanguageInfo("vi", "Tiếng Việt", "Vietnamese", false),
        new LanguageInfo("th", "ไทย", "Thai", false),
    };

    private static readonly Lazy<Dictionary<string, string>> FallbackTable =
        new(() => Load(FallbackLanguage) ?? new Dictionary<string, string>(StringComparer.Ordinal));

    private Dictionary<string, string> _table = new(StringComparer.Ordinal);
    private LanguageInfo _current = Languages[0];

    public static Loc Current { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public LanguageInfo CurrentLanguage => _current;

    public FlowDirection FlowDirection => _current.RightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    /// <summary>Indexer so XAML can bind a key directly. Unknown keys fall back to English, then to the key itself.</summary>
    public string this[string key]
    {
        get
        {
            if (_table.TryGetValue(key, out var value))
            {
                return value;
            }

            return FallbackTable.Value.TryGetValue(key, out var fallback) ? fallback : key;
        }
    }

    public string Format(string key, params object?[] args)
    {
        string template = this[key];
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public void SetLanguage(string code)
    {
        var info = Resolve(code);
        var table = Load(info.Code) ?? new Dictionary<string, string>(StringComparer.Ordinal);

        _current = info;
        _table = table;

        var culture = TryCreateCulture(info.Code);
        if (culture is not null)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FlowDirection)));
    }

    /// <summary>Best match for the operating system language, e.g. zh-TW maps to zh-Hant.</summary>
    public static LanguageInfo DetectSystemLanguage()
    {
        var culture = CultureInfo.CurrentUICulture;

        if (culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
        {
            string name = culture.Name;
            bool traditional =
                name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("-TW", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("-HK", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("-MO", StringComparison.OrdinalIgnoreCase);

            return Resolve(traditional ? "zh-Hant" : "zh-Hans");
        }

        if (culture.TwoLetterISOLanguageName.Equals("pt", StringComparison.OrdinalIgnoreCase))
        {
            return Resolve("pt-BR");
        }

        var exact = Languages.FirstOrDefault(l => l.Code.Equals(culture.Name, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        var byLanguage = Languages.FirstOrDefault(l =>
            l.Code.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase));

        return byLanguage ?? Languages[0];
    }

    public static LanguageInfo Resolve(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Languages[0];
        }

        return Languages.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase))
            ?? Languages.FirstOrDefault(l => code.StartsWith(l.Code + "-", StringComparison.OrdinalIgnoreCase))
            ?? Languages[0];
    }

    private static CultureInfo? TryCreateCulture(string code)
    {
        try
        {
            return CultureInfo.GetCultureInfo(code);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static Dictionary<string, string>? Load(string code)
    {
        var assembly = Assembly.GetExecutingAssembly();
        string resourceName = $"FileMerge.Localization.Strings.{code}.json";

        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(stream);
            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    table[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }

            return table;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
