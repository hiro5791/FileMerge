using System.Diagnostics;
using System.Reflection;

namespace FileMerge;

/// <summary>Version information, read once from the running assembly.</summary>
public static class AppInfo
{
    private static readonly Lazy<string> VersionValue = new(ReadVersion);

    /// <summary>Three-part version, e.g. "1.0.0". Empty only if the assembly carries none.</summary>
    public static string Version => VersionValue.Value;

    public static string DisplayVersion => Version.Length == 0 ? string.Empty : "v" + Version;

    private static string ReadVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // InformationalVersion is the one the build stamps from <Version>, but it carries the
        // source revision after a '+', which is noise in a window header.
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            int plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        try
        {
            string? product = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? string.Empty).ProductVersion;
            if (!string.IsNullOrWhiteSpace(product))
            {
                return product;
            }
        }
        catch
        {
            // Falls through to the assembly version below.
        }

        var version = assembly.GetName().Version;
        return version is null ? string.Empty : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
