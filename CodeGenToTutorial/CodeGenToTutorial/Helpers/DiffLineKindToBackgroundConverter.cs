using CodeGenToTutorial.Models;

using Microsoft.UI;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace CodeGenToTutorial.Helpers;

public class DiffLineKindToBackgroundConverter : IValueConverter
{
    private static readonly SolidColorBrush AddedBrush = new(Color.FromArgb(40, 46, 160, 67));
    private static readonly SolidColorBrush RemovedBrush = new(Color.FromArgb(40, 218, 54, 51));
    private static readonly SolidColorBrush UnchangedBrush = new(Colors.Transparent);

    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is DiffLineKind kind
            ? kind switch
            {
                DiffLineKind.Added => AddedBrush,
                DiffLineKind.Removed => RemovedBrush,
                _ => UnchangedBrush,
            }
            : UnchangedBrush;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
