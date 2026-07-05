using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

using Windows.UI;

namespace AiDev.WinUI.Converters;

static class ConverterHelpers
{
    public static SolidColorBrush BrushFromHex(string hex)
    {
        try
        {
            if (hex.StartsWith('#') && hex.Length == 7)
            {
                var r = System.Convert.ToByte(hex.Substring(1, 2), 16);
                var g = System.Convert.ToByte(hex.Substring(3, 2), 16);
                var b = System.Convert.ToByte(hex.Substring(5, 2), 16);
                return new SolidColorBrush(Color.FromArgb(255, r, g, b));
            }
        }
        catch { }
        return new SolidColorBrush(Colors.Gray);
    }
}
