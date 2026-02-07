using System;
using System.IO;
using System.Windows;
using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;

namespace MeirDownloader.Desktop;

public partial class MainWindow : Window
{
    private readonly IMeirDownloaderService _downloaderService;
    private string _downloadPath;

    public MainWindow()
    {
        InitializeComponent();
        _downloaderService = new MeirDownloaderService();
        _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "מוריד שיעורים");
        
        LoadRabbis();
    }

    private async void LoadRabbis()
    {
        try
        {
            StatusText.Text = "טוען רבים...";
            var rabbis = await _downloaderService.GetRabbisAsync();
            RabbiListBox.ItemsSource = rabbis;
            StatusText.Text = $"טוענו {rabbis.Count} רבים";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה: {ex.Message}";
            MessageBox.Show($"Failed to load rabbis: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RabbiListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RabbiListBox.SelectedItem is Rabbi rabbi)
        {
            try
            {
                StatusText.Text = "טוען סדרות...";
                var series = await _downloaderService.GetSeriesAsync(rabbi.Id);
                SeriesListBox.ItemsSource = series;
                StatusText.Text = $"טוענו {series.Count} סדרות";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה: {ex.Message}";
            }
        }
    }

    private async void SeriesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SeriesListBox.SelectedItem is Series series && RabbiListBox.SelectedItem is Rabbi rabbi)
        {
            try
            {
                StatusText.Text = "טוען שיעורים...";
                var lessons = await _downloaderService.GetLessonsAsync(rabbi.Id, series.Id);
                LessonsGrid.ItemsSource = lessons;
                StatusText.Text = $"טוענו {lessons.Count} שיעורים";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה: {ex.Message}";
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RabbiListBox.ItemsSource = null;
        SeriesListBox.ItemsSource = null;
        LessonsGrid.ItemsSource = null;
        LoadRabbis();
    }

    private void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Download functionality coming soon!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
