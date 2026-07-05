using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AiDev.WinUI.Converters;

public class AgentStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is AgentStatus s ? s.DisplayName : "Idle";

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
