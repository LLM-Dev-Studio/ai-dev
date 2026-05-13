using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace AiDev.WinUI.Converters;

public class DateTimeToDisplayStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is DateTime dt)
        {
            var format = parameter as string ?? "MMM d";
            return dt.ToLocalTime().ToString(format);
        }
        return parameter as string == "HH:mm" ? string.Empty : "—";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
