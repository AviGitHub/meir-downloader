using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;

namespace MeirDownloader.Desktop;

public partial class MainWindow : Window
{
    private readonly IMeirDownloaderService _downloaderService;
    private string _downloadPath;
    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;

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
            StatusText.Text = "טוען רבנים...";
            var rabbis = await _downloaderService.GetRabbisAsync();
            RabbiListBox.ItemsSource = rabbis;
            StatusText.Text = $"נטענו {rabbis.Count} רבנים";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה: {ex.Message}";
            MessageBox.Show($"Failed to load rabbis: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RabbiListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RabbiListBox.SelectedItem is Rabbi rabbi)
        {
            try
            {
                StatusText.Text = "טוען סדרות...";
                SeriesListBox.ItemsSource = null;
                LessonsGrid.ItemsSource = null;
                UpdateDownloadButtonStates();

                var series = await _downloaderService.GetSeriesAsync(rabbi.Id);
                SeriesListBox.ItemsSource = series;
                StatusText.Text = $"נטענו {series.Count} סדרות";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה: {ex.Message}";
            }
        }
    }

    private async void SeriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SeriesListBox.SelectedItem is Series series && RabbiListBox.SelectedItem is Rabbi rabbi)
        {
            try
            {
                StatusText.Text = "טוען שיעורים...";
                var lessons = await _downloaderService.GetLessonsAsync(rabbi.Id, series.Id);

                // Resolve RabbiName and SeriesName from the selected items
                foreach (var lesson in lessons)
                {
                    lesson.RabbiName = rabbi.Name;
                    lesson.SeriesName = series.Name;
                }

                LessonsGrid.ItemsSource = lessons;
                StatusText.Text = $"נטענו {lessons.Count} שיעורים";
                UpdateDownloadButtonStates();
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
        UpdateDownloadButtonStates();
        LoadRabbis();
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is Lesson lesson)
        {
            if (_isDownloading)
            {
                MessageBox.Show("הורדה כבר מתבצעת. אנא המתן או בטל את ההורדה הנוכחית.", "הורדה פעילה",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                SetDownloadingState(true);
                _downloadCts = new CancellationTokenSource();

                var progress = new Progress<DownloadProgress>(p =>
                {
                    ProgressBar.Value = p.ProgressPercentage;
                    StatusText.Text = $"מוריד: {p.LessonTitle} ({p.ProgressPercentage}%)";
                });

                StatusText.Text = $"מוריד: {lesson.Title}...";
                var filePath = await _downloaderService.DownloadLessonAsync(lesson, _downloadPath, progress, _downloadCts.Token);

                ProgressBar.Value = 100;
                StatusText.Text = $"הורדה הושלמה: {Path.GetFileName(filePath)}";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "ההורדה בוטלה";
                ProgressBar.Value = 0;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה בהורדה: {ex.Message}";
                ProgressBar.Value = 0;
                MessageBox.Show($"שגיאה בהורדה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SetDownloadingState(false);
                _downloadCts?.Dispose();
                _downloadCts = null;
            }
        }
    }

    private async void DownloadSeriesButton_Click(object sender, RoutedEventArgs e)
    {
        if (RabbiListBox.SelectedItem is not Rabbi rabbi || SeriesListBox.SelectedItem is not Series series)
        {
            MessageBox.Show("אנא בחר רב וסדרה להורדה.", "בחירה חסרה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_isDownloading)
        {
            MessageBox.Show("הורדה כבר מתבצעת. אנא המתן או בטל את ההורדה הנוכחית.", "הורדה פעילה",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetDownloadingState(true);
            _downloadCts = new CancellationTokenSource();

            StatusText.Text = "טוען את כל השיעורים בסדרה...";
            var allLessons = await _downloaderService.GetAllLessonsAsync(rabbi.Id, series.Id, _downloadCts.Token);

            // Set rabbi and series names
            foreach (var lesson in allLessons)
            {
                lesson.RabbiName = rabbi.Name;
                lesson.SeriesName = series.Name;
            }

            if (allLessons.Count == 0)
            {
                StatusText.Text = "לא נמצאו שיעורים בסדרה זו";
                return;
            }

            StatusText.Text = $"מוריד {allLessons.Count} שיעורים מסדרה: {series.Name}";
            await DownloadLessonsWithNumbering(allLessons, _downloadCts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "ההורדה בוטלה";
            ProgressBar.Value = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה בהורדה: {ex.Message}";
            ProgressBar.Value = 0;
            MessageBox.Show($"שגיאה בהורדה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetDownloadingState(false);
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private async void DownloadAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (LessonsGrid.ItemsSource is not IEnumerable<Lesson> lessons)
        {
            MessageBox.Show("אין שיעורים להורדה.", "אין נתונים", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var lessonList = lessons.ToList();
        if (lessonList.Count == 0)
        {
            MessageBox.Show("אין שיעורים להורדה.", "אין נתונים", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_isDownloading)
        {
            MessageBox.Show("הורדה כבר מתבצעת. אנא המתן או בטל את ההורדה הנוכחית.", "הורדה פעילה",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            SetDownloadingState(true);
            _downloadCts = new CancellationTokenSource();

            StatusText.Text = $"מוריד {lessonList.Count} שיעורים...";
            await DownloadLessonsWithNumbering(lessonList, _downloadCts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "ההורדה בוטלה";
            ProgressBar.Value = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה בהורדה: {ex.Message}";
            ProgressBar.Value = 0;
            MessageBox.Show($"שגיאה בהורדה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetDownloadingState(false);
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
        {
            _downloadCts.Cancel();
            StatusText.Text = "מבטל הורדה...";
        }
    }

    private async Task DownloadLessonsWithNumbering(List<Lesson> lessons, CancellationToken ct)
    {
        int totalLessons = lessons.Count;
        int completedLessons = 0;
        int failedLessons = 0;

        for (int i = 0; i < totalLessons; i++)
        {
            ct.ThrowIfCancellationRequested();

            var lesson = lessons[i];
            int index = i + 1;

            var progress = new Progress<DownloadProgress>(p =>
            {
                // Calculate overall progress: completed lessons + current lesson progress
                double overallProgress = ((double)completedLessons / totalLessons * 100) +
                                         ((double)p.ProgressPercentage / totalLessons);
                ProgressBar.Value = Math.Min(overallProgress, 100);
                StatusText.Text = $"מוריד ({index}/{totalLessons}): {p.LessonTitle} ({p.ProgressPercentage}%)";
            });

            try
            {
                await _downloaderService.DownloadLessonAsync(lesson, _downloadPath, index, progress, ct);
                completedLessons++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedLessons++;
                System.Diagnostics.Debug.WriteLine($"Failed to download lesson {index}: {ex.Message}");
                // Continue with next lesson
            }
        }

        ProgressBar.Value = 100;
        if (failedLessons > 0)
        {
            StatusText.Text = $"הורדה הושלמה: {completedLessons} הצליחו, {failedLessons} נכשלו מתוך {totalLessons}";
        }
        else
        {
            StatusText.Text = $"הורדה הושלמה: {completedLessons} שיעורים הורדו בהצלחה";
        }
    }

    private void SetDownloadingState(bool isDownloading)
    {
        _isDownloading = isDownloading;
        CancelDownloadButton.IsEnabled = isDownloading;
        CancelDownloadButton.Visibility = isDownloading ? Visibility.Visible : Visibility.Collapsed;

        if (!isDownloading)
        {
            UpdateDownloadButtonStates();
        }
        else
        {
            DownloadSeriesButton.IsEnabled = false;
            DownloadAllButton.IsEnabled = false;
        }
    }

    private void UpdateDownloadButtonStates()
    {
        bool hasLessons = LessonsGrid.ItemsSource is IEnumerable<Lesson> lessons && lessons.Any();
        bool hasSeriesSelected = SeriesListBox.SelectedItem is Series && RabbiListBox.SelectedItem is Rabbi;

        DownloadSeriesButton.IsEnabled = hasSeriesSelected && !_isDownloading;
        DownloadAllButton.IsEnabled = hasLessons && !_isDownloading;
    }
}
