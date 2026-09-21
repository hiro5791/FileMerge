using System.Windows.Data;
using System.Windows.Markup;

namespace FileMerge.Localization;

/// <summary>
/// XAML shorthand: <c>Content="{loc:T Action.Merge}"</c>. Produces a binding rather than a
/// literal, so switching language updates every label in place with no window reload.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TExtension : MarkupExtension
{
    public TExtension()
    {
    }

    public TExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = Loc.Current,
            Mode = BindingMode.OneWay,
        };

        return binding.ProvideValue(serviceProvider);
    }
}
