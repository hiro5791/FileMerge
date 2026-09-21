using FileMerge.Localization;

namespace FileMerge.ViewModels;

/// <summary>A combo-box entry that carries an enum value and a translatable label.</summary>
public sealed class EnumOption<T>
    where T : struct, Enum
{
    public EnumOption(T value, string labelKey)
    {
        Value = value;
        LabelKey = labelKey;
    }

    public T Value { get; }

    public string LabelKey { get; }

    public string Display => Loc.Current[LabelKey];

    public override string ToString() => Display;
}
