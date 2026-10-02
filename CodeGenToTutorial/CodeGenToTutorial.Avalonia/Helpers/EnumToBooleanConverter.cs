using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Styling;

namespace CodeGenToTutorial.Avalonia.Helpers;

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string variantName)
        {
            var enumValue = ParseThemeVariant(variantName);
            return enumValue.Equals(value);
        }

        throw new ArgumentException("Converter parameter must be a ThemeVariant name.");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string variantName)
        {
            return ParseThemeVariant(variantName);
        }

        throw new ArgumentException("Converter parameter must be a ThemeVariant name.");
    }

    private static ThemeVariant ParseThemeVariant(string variantName) => variantName switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };
}
