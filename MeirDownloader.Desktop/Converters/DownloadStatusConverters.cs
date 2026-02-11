using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MeirDownloader.Desktop.ViewModels;

namespace MeirDownloader.Desktop.Converters;

public class DownloadStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus status)
        {
            return status switch
            {
                DownloadStatus.Ready => new SolidColorBrush(Color.FromRgb(0x34, 0x98, 0xDB)),      // Blue
                DownloadStatus.Downloading => new SolidColorBrush(Color.FromRgb(0xF3, 0x9C, 0x12)), // Orange
                DownloadStatus.Completed => new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),   // Green
                DownloadStatus.Error => new SolidColorBrush(Color.FromRgb(0xE7, 0x4C, 0x3C)),       // Red
                DownloadStatus.Skipped => new SolidColorBrush(Color.FromRgb(0x27, 0xAE, 0x60)),     // Green (same as completed)
                _ => new SolidColorBrush(Colors.Gray)
            };
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class DownloadStatusToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DownloadStatus status)
        {
            return status == DownloadStatus.Ready || status == DownloadStatus.Error
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
        }
        return System.Windows.Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
