namespace FileMerge.Models;

public sealed record MergeResult(
    bool Succeeded,
    string OutputPath,
    int FileCount,
    long BytesWritten,
    TimeSpan Elapsed,
    IReadOnlyList<string> Warnings)
{
    public static MergeResult Failure(string outputPath, string message) =>
        new(false, outputPath, 0, 0, TimeSpan.Zero, new[] { message });
}

/// <summary>Progress pushed to the UI while a merge runs.</summary>
public sealed record MergeProgress(int FileIndex, int FileCount, string CurrentFile, double Fraction);
