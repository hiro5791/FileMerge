using System.IO;

namespace FileMerge.Services;

public sealed class FolderScanOptions
{
    /// <summary>Semicolon- or comma-separated wildcards, e.g. "*.log; *.txt". Empty means everything.</summary>
    public string Filter { get; init; } = "*.*";

    public bool Recursive { get; init; } = true;

    public bool IncludeHidden { get; init; }

    /// <summary>Folder names skipped entirely during a recursive scan.</summary>
    public IReadOnlyCollection<string> ExcludedFolders { get; init; } =
        new[] { ".git", "node_modules", "bin", "obj", ".vs" };
}

public static class FolderScanner
{
    public static IReadOnlyList<string> Scan(string folder, FolderScanOptions options, CancellationToken token = default)
    {
        var patterns = ParseFilter(options.Filter);
        var results = new List<string>();
        var excluded = new HashSet<string>(options.ExcludedFolders, StringComparer.OrdinalIgnoreCase);

        var queue = new Queue<string>();
        queue.Enqueue(folder);

        while (queue.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            string current = queue.Dequeue();

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                continue;
            }

            foreach (string file in files)
            {
                token.ThrowIfCancellationRequested();

                if (!options.IncludeHidden && IsHidden(file))
                {
                    continue;
                }

                if (Matches(Path.GetFileName(file), patterns))
                {
                    results.Add(file);
                }
            }

            if (!options.Recursive)
            {
                continue;
            }

            IEnumerable<string> subdirectories;
            try
            {
                subdirectories = Directory.EnumerateDirectories(current);
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string dir in subdirectories)
            {
                string name = Path.GetFileName(dir);
                if (excluded.Contains(name))
                {
                    continue;
                }

                if (!options.IncludeHidden && IsHidden(dir))
                {
                    continue;
                }

                queue.Enqueue(dir);
            }
        }

        results.Sort(NaturalComparer.Instance);
        return results;
    }

    private static bool IsHidden(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.Hidden) || attributes.HasFlag(FileAttributes.System);
        }
        catch
        {
            return false;
        }
    }

    public static IReadOnlyList<string> ParseFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return new[] { "*" };
        }

        var parts = filter
            .Split(new[] { ';', ',', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToArray();

        return parts.Length == 0 ? new[] { "*" } : parts;
    }

    private static bool Matches(string fileName, IReadOnlyList<string> patterns)
    {
        foreach (string pattern in patterns)
        {
            if (pattern is "*" or "*.*")
            {
                return true;
            }

            if (WildcardMatch(fileName, pattern))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Wildcard match for * and ?. Written by hand rather than via Regex so a user-typed
    /// filter can never throw or blow up on a pathological pattern.
    /// </summary>
    internal static bool WildcardMatch(string text, string pattern)
    {
        int t = 0, p = 0, starIndex = -1, match = 0;

        while (t < text.Length)
        {
            if (p < pattern.Length &&
                (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(text[t])))
            {
                t++;
                p++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                starIndex = p;
                match = t;
                p++;
            }
            else if (starIndex != -1)
            {
                p = starIndex + 1;
                match++;
                t = match;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
