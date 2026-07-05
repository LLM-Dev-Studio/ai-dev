using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AiDev.WinUI.Converters;

public class PriorityColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is Priority p ? ConverterHelpers.BrushFromHex(p.ColorHex) : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
