using System.Globalization;

namespace FileMerge.Services;

/// <summary>
/// Orders strings the way a person reads file names: "part2" before "part10", and
/// "file.9" before "file.10". Plain ordinal sorting gets split archives wrong, which
/// silently produces a corrupt merge, so this is the default everywhere.
/// </summary>
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        int i = 0, j = 0;

        while (i < x.Length && j < y.Length)
        {
            char cx = x[i];
            char cy = y[j];

            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                int startX = i, startY = j;
                while (i < x.Length && char.IsDigit(x[i]))
                {
                    i++;
                }

                while (j < y.Length && char.IsDigit(y[j]))
                {
                    j++;
                }

                var numX = x.AsSpan(startX, i - startX).TrimStart('0');
                var numY = y.AsSpan(startY, j - startY).TrimStart('0');

                if (numX.Length != numY.Length)
                {
                    return numX.Length - numY.Length;
                }

                int digits = numX.SequenceCompareTo(numY);
                if (digits != 0)
                {
                    return digits;
                }

                // Equal values: "01" sorts before "1" so the order stays stable.
                int widthX = i - startX;
                int widthY = j - startY;
                if (widthX != widthY)
                {
                    return widthY - widthX;
                }

                continue;
            }

            int cmp = string.Compare(
                x[i].ToString(),
                y[j].ToString(),
                CultureInfo.CurrentCulture,
                CompareOptions.IgnoreCase | CompareOptions.StringSort);

            if (cmp != 0)
            {
                return cmp;
            }

            i++;
            j++;
        }

        int remaining = (x.Length - i) - (y.Length - j);
        return remaining != 0 ? remaining : string.CompareOrdinal(x, y);
    }
}
