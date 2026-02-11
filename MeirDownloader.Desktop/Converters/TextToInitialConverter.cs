using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace MeirDownloader.Desktop.Converters;

public class TextToInitialConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text && !string.IsNullOrEmpty(text))
        {
            return text.Trim().FirstOrDefault().ToString();
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
