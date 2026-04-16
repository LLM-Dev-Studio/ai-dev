using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AiDev.Desktop.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Visible;
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is not Visibility.Visible;
}

/// <summary>Returns Visible when value is non-null/non-empty, Collapsed when null/empty.</summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is null or "" ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public class PriorityColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Priority p)
        {
            return p.Value switch
            {
                "critical" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // red
                "high"     => new SolidColorBrush(Color.FromRgb(245, 158, 11)),  // amber
                "normal"   => new SolidColorBrush(Color.FromRgb(59, 130, 246)),  // blue
                _          => new SolidColorBrush(Color.FromRgb(107, 114, 128)), // gray
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public class PriorityTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public class AgentStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AgentStatus s)
        {
            return s.Value switch
            {
                "running" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // green
                "error"   => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // red
                _         => new SolidColorBrush(Color.FromRgb(107, 114, 128)), // gray idle
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}

public class AgentStatusTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is AgentStatus s)
            return s.Value switch
            {
                "running" => "Running",
                "error" => "Error",
                _ => "Idle"
            };
        return "Idle";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => DependencyProperty.UnsetValue;
}
