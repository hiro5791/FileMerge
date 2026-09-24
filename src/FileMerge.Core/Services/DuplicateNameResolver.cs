using System.IO;
using FileMerge.Models;

namespace FileMerge.Services;

/// <summary>
/// Works out how to tell apart files that share a name. Merging five files all called
/// report.txt is a normal thing to want, and a list of five identical rows is useless, so each
/// one is labelled with the shortest tail of its folder path that is unique among them.
/// </summary>
public static class DuplicateNameResolver
{
    private const int MaxSegments = 6;

    public static void Apply(IReadOnlyList<FileEntry> entries)
    {
        foreach (var group in entries.GroupBy(e => e.FileName, StringComparer.OrdinalIgnoreCase))
        {
            var members = group.ToList();

            if (members.Count < 2)
            {
                foreach (var entry in members)
                {
                    entry.DuplicateHint = null;
                }

                continue;
            }

            Label(members);
        }
    }

    private static void Label(List<FileEntry> members)
    {
        // Widen the tail one folder at a time until every member reads differently.
        for (int depth = 1; depth <= MaxSegments; depth++)
        {
            var labels = members.Select(m => TrailingSegments(m.DirectoryName, depth)).ToList();

            if (labels.Distinct(StringComparer.OrdinalIgnoreCase).Count() == members.Count)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    members[i].DuplicateHint = labels[i];
                }

                return;
            }
        }

        // Same name in the same folder cannot happen, so this is only reached for paths
        // deeper than the limit; the whole folder is then the clearest thing to show.
        foreach (var member in members)
        {
            member.DuplicateHint = member.DirectoryName;
        }
    }

    /// <summary>The last <paramref name="count"/> folder names of a path, joined back together.</summary>
    public static string TrailingSegments(string directory, int count)
    {
        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        var segments = directory.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length <= count)
        {
            return directory;
        }

        return string.Join(Path.DirectorySeparatorChar, segments[^count..]);
    }
}
