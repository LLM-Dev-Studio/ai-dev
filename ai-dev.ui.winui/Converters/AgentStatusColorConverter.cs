using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace AiDev.WinUI.Converters;

public class AgentStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is AgentStatus s ? ConverterHelpers.BrushFromHex(s.ColorHex) : new SolidColorBrush(Colors.Gray);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
