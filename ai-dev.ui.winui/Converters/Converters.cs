using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace AiDev.WinUI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is Visibility.Visible;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is not Visibility.Visible;
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is not true;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => value is not true;
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}

public class StringHexToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string hex && hex.StartsWith('#') && hex.Length == 7)
        {
            try
            {
                var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(5, 2), 16);
                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
            catch (FormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Invalid hex color '{hex}': {ex.Message}");
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}

public class PriorityColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Priority p)
        {
            return p.Value switch
            {
                "critical" => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)),
                "high"     => new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)),
                "normal"   => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                _          => new SolidColorBrush(Color.FromArgb(255, 107, 114, 128)),
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}

public class PriorityTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is Priority p)
            return p.Value switch
            {
                "critical" => "Critical",
                "high" => "High",
                "normal" => "Normal",
                _ => "Low"
            };
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}

public class AgentStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is AgentStatus s)
        {
            return s.Value switch
            {
                "running" => new SolidColorBrush(Color.FromArgb(255, 34, 197, 94)),
                "error"   => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)),
                _         => new SolidColorBrush(Color.FromArgb(255, 107, 114, 128)),
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}

public class AgentStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is AgentStatus s)
            return s.Value switch
            {
                "running" => "Running",
                "error"   => "Error",
                _ => "Idle"
            };
        return "Idle";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => DependencyProperty.UnsetValue;
}
