using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace Paarde.Avalonia.Helpers;

// Despite the name, this originally only handled ThemeVariant - which isn't a real CLR enum (it's an
// Avalonia class with static Light/Dark/Default members), so it needed its own parsing. T6.3.3 added
// a second use (LlmRunnerKind radio buttons), which *is* a real enum - handled generically via
// Enum.Parse below rather than growing another ThemeVariant-shaped special case.
public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string name)
        {
            throw new ArgumentException("Converter parameter must be a member name.");
        }

        return value switch
        {
            ThemeVariant => ParseThemeVariant(name).Equals(value),
            Enum enumValue => Enum.Parse(enumValue.GetType(), name).Equals(enumValue),
            _ => false,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string name)
        {
            throw new ArgumentException("Converter parameter must be a member name.");
        }

        return targetType == typeof(ThemeVariant) ? ParseThemeVariant(name) : Enum.Parse(targetType, name);
    }

    private static ThemeVariant ParseThemeVariant(string variantName) => variantName switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
