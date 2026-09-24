using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FileMerge.Models;

namespace FileMerge.Services;

public sealed class AppSettings
{
    public string? Language { get; set; }

    public string Theme { get; set; } = "System";

    public bool EnsureTrailingNewline { get; set; }

    public bool RemoveInnerBoms { get; set; }

    public ExistingFileAction ExistingFile { get; set; } = ExistingFileAction.Ask;

    /// <summary>Where the file and folder pickers open. Follows wherever the user last worked.</summary>
    public string LastFolder { get; set; } = string.Empty;

    public string FolderFilter { get; set; } = "*.*";

    public bool FolderRecursive { get; set; } = true;

    public bool FolderIncludeHidden { get; set; }

    public double WindowWidth { get; set; } = 1060;

    public double WindowHeight { get; set; } = 700;
}

/// <summary>
/// Loads and saves <see cref="AppSettings"/>.
/// <para>
/// Portable mode: if a file named <c>portable.txt</c> or <c>FileMerge.settings.json</c> sits next
/// to the executable, settings live there so a USB copy of the app carries its own configuration.
/// Otherwise they go to LocalAppData, which also keeps the Store build inside its own container.
/// </para>
/// </summary>
public sealed class SettingsService
{
    private const string FileName = "FileMerge.settings.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public SettingsService()
    {
        _path = ResolvePath();
    }

    public string Path => _path;

    public bool IsPortable { get; private set; }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // A corrupt or unreadable settings file must never stop the app from starting.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            string? directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(settings, Options));
        }
        catch (Exception)
        {
            // Read-only media is a normal situation for a portable app; settings simply do not persist.
        }
    }

    private string ResolvePath()
    {
        string exeDirectory = AppContext.BaseDirectory;
        string beside = System.IO.Path.Combine(exeDirectory, FileName);

        if (File.Exists(beside) || File.Exists(System.IO.Path.Combine(exeDirectory, "portable.txt")))
        {
            IsPortable = true;
            return beside;
        }

        string roamingRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return System.IO.Path.Combine(roamingRoot, "FileMerge", FileName);
    }
}
