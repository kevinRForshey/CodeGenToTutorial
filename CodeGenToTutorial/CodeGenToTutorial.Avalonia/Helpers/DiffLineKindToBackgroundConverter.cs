using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

using CodeGenToTutorial.Core.Models;

namespace CodeGenToTutorial.Avalonia.Helpers;

public class DiffLineKindToBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush AddedBrush = new(Color.FromArgb(40, 46, 160, 67));
    private static readonly SolidColorBrush RemovedBrush = new(Color.FromArgb(40, 218, 54, 51));
    private static readonly SolidColorBrush UnchangedBrush = new(Colors.Transparent);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DiffLineKind kind
            ? kind switch
            {
                DiffLineKind.Added => AddedBrush,
                DiffLineKind.Removed => RemovedBrush,
                _ => UnchangedBrush,
            }
            : UnchangedBrush;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
