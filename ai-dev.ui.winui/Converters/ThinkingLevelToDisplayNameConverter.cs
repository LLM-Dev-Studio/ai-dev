using AiDev.Executors;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AiDev.WinUI.Converters;

public class ThinkingLevelToDisplayNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is ThinkingLevel level ? level.DisplayName : "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
