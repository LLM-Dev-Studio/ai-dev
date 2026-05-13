using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AiDev.WinUI.Converters;

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var isNullOrEmpty = value is null || (value is string s && string.IsNullOrWhiteSpace(s));
        var inverse = parameter is string text && text.Equals("inverse", StringComparison.OrdinalIgnoreCase);
        return inverse
            ? (isNullOrEmpty ? Visibility.Visible : Visibility.Collapsed)
            : (isNullOrEmpty ? Visibility.Collapsed : Visibility.Visible);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
