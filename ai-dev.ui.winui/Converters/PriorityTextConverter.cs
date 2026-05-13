using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AiDev.WinUI.Converters;

public class PriorityTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is Priority p ? p.DisplayName : "";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
