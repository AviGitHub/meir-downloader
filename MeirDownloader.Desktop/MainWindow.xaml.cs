using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;
using MeirDownloader.Desktop.Services;
using MeirDownloader.Desktop.ViewModels;

namespace MeirDownloader.Desktop;

public partial class MainWindow : Window
{
    private readonly IMeirDownloaderService _downloaderService;
    private readonly DownloadManager _downloadManager;
    private string _downloadPath;
    private CancellationTokenSource? _loadingCts;
    private CancellationTokenSource? _rabbiLoadingCts;
    private CancellationTokenSource? _seriesLoadingCts;
    private bool _isDownloading;
    private List<LessonViewModel>? _currentLessons;
    private readonly ObservableCollection<Rabbi> _rabbis = new();
    private readonly ObservableCollection<Series> _seriesList = new();

    public MainWindow()
    {
        InitializeComponent();
        _downloaderService = new MeirDownloaderService();
        _downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "מוריד שיעורים");
        _downloadManager = new DownloadManager(_downloaderService, 4);

        _downloadManager.OverallProgressChanged += (completed, total) =>
        {
            Dispatcher.InvokeAsync(() => UpdateOverallProgress(completed, total));
        };

        DownloadPathText.Text = $"תיקיית הורדה: {_downloadPath}";

        LoadRabbis();
    }

    private async void LoadRabbis()
    {
        try
        {
            _rabbiLoadingCts?.Cancel();
            _rabbiLoadingCts = new CancellationTokenSource();
            var ct = _rabbiLoadingCts.Token;

            RabbiLoadingBar.Visibility = Visibility.Visible;
            _rabbis.Clear();
            RabbiListBox.ItemsSource = _rabbis;
            StatusText.Text = "טוען רבנים...";

            await foreach (var page in _downloaderService.GetRabbisStreamAsync(ct))
            {
                foreach (var rabbi in page)
                {
                    _rabbis.Add(rabbi);
                }
                StatusText.Text = $"נטענו {_rabbis.Count} רבנים...";
            }

            StatusText.Text = $"נטענו {_rabbis.Count} רבנים";
        }
        catch (OperationCanceledException)
        {
            // Loading was cancelled
        }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה: {ex.Message}";
            MessageBox.Show($"Failed to load rabbis: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RabbiLoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void RabbiListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RabbiListBox.SelectedItem is Rabbi rabbi)
        {
            try
            {
                _seriesLoadingCts?.Cancel();
                _seriesLoadingCts = new CancellationTokenSource();
                var ct = _seriesLoadingCts.Token;

                SeriesLoadingBar.Visibility = Visibility.Visible;
                StatusText.Text = $"טוען סדרות עבור {rabbi.Name}...";
                _seriesList.Clear();
                SeriesListBox.ItemsSource = _seriesList;
                LessonsGrid.ItemsSource = null;
                _currentLessons = null;
                UpdateDownloadButtonStates();

                await foreach (var page in _downloaderService.GetSeriesStreamAsync(rabbi.Id, ct))
                {
                    foreach (var series in page.Where(s => s.Count > 0))
                    {
                        // Insert in sorted order (alphabetically by name)
                        var index = 0;
                        while (index < _seriesList.Count && string.Compare(_seriesList[index].Name, series.Name, StringComparison.Ordinal) < 0)
                            index++;
                        _seriesList.Insert(index, series);
                    }
                    StatusText.Text = $"נטענו {_seriesList.Count} סדרות עבור {rabbi.Name}...";
                }

                StatusText.Text = $"נטענו {_seriesList.Count} סדרות עבור {rabbi.Name}";
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled (user selected a different rabbi)
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה: {ex.Message}";
            }
            finally
            {
                SeriesLoadingBar.Visibility = Visibility.Collapsed;
            }
        }
    }

    private async void SeriesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SeriesListBox.SelectedItem is Series series && RabbiListBox.SelectedItem is Rabbi rabbi)
        {
            try
            {
                LessonsLoadingBar.Visibility = Visibility.Visible;
                StatusText.Text = "טוען את כל השיעורים...";
                LessonsGrid.ItemsSource = null;
                _currentLessons = null;

                // Cancel any previous loading operation
                _loadingCts?.Cancel();
                _loadingCts = new CancellationTokenSource();

                // Fetch ALL lessons (all pages) sorted by date ascending
                var lessons = await _downloaderService.GetAllLessonsAsync(rabbi.Id, series.Id, _loadingCts.Token);

                // Set rabbi and series names
                foreach (var lesson in lessons)
                {
                    lesson.RabbiName = rabbi.Name;
                    lesson.SeriesName = series.Name;
                }

                // Create numbered view models
                _currentLessons = lessons
                    .Select((lesson, index) => new LessonViewModel(lesson, index + 1))
                    .ToList();

                // Check which lessons are already downloaded
                foreach (var lessonVm in _currentLessons)
                {
                    lessonVm.CheckIfAlreadyDownloaded(_downloadPath);
                }

                LessonsGrid.ItemsSource = _currentLessons;
                StatusText.Text = $"נטענו {_currentLessons.Count} שיעורים";
                UpdateDownloadButtonStates();
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled (user selected a different series)
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה: {ex.Message}";
            }
            finally
            {
                LessonsLoadingBar.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _rabbiLoadingCts?.Cancel();
        _seriesLoadingCts?.Cancel();
        _rabbis.Clear();
        _seriesList.Clear();
        RabbiListBox.ItemsSource = null;
        SeriesListBox.ItemsSource = null;
        LessonsGrid.ItemsSource = null;
        _currentLessons = null;
        UpdateDownloadButtonStates();
        LoadRabbis();
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is LessonViewModel lessonVm)
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

                // Download just this single lesson using the download manager
                var singleList = new List<LessonViewModel> { lessonVm };
                await _downloadManager.DownloadAllAsync(singleList, _downloadPath);

                StatusText.Text = lessonVm.Status == DownloadStatus.Completed
                    ? $"הורדה הושלמה: {lessonVm.Title}"
                    : $"הורדה נכשלה: {lessonVm.Title}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"שגיאה בהורדה: {ex.Message}";
            }
            finally
            {
                SetDownloadingState(false);
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
            StatusText.Text = "טוען את כל השיעורים בסדרה...";

            // If we already have lessons loaded, use them; otherwise fetch
            if (_currentLessons == null || _currentLessons.Count == 0)
            {
                _loadingCts?.Cancel();
                _loadingCts = new CancellationTokenSource();

                var allLessons = await _downloaderService.GetAllLessonsAsync(rabbi.Id, series.Id, _loadingCts.Token);

                foreach (var lesson in allLessons)
                {
                    lesson.RabbiName = rabbi.Name;
                    lesson.SeriesName = series.Name;
                }

                _currentLessons = allLessons
                    .Select((lesson, index) => new LessonViewModel(lesson, index + 1))
                    .ToList();

                LessonsGrid.ItemsSource = _currentLessons;
            }

            if (_currentLessons.Count == 0)
            {
                StatusText.Text = "לא נמצאו שיעורים בסדרה זו";
                return;
            }

            StatusText.Text = $"מוריד {_currentLessons.Count} שיעורים מסדרה: {series.Name}";
            OverallProgressBar.Value = 0;
            OverallProgressText.Text = "";

            await _downloadManager.DownloadAllAsync(_currentLessons, _downloadPath);

            var completed = _currentLessons.Count(l => l.Status == DownloadStatus.Completed);
            var skipped = _currentLessons.Count(l => l.Status == DownloadStatus.Skipped);
            var errors = _currentLessons.Count(l => l.Status == DownloadStatus.Error);

            StatusText.Text = $"הורדה הושלמה: {completed} הורדו, {skipped} כבר קיימים, {errors} שגיאות";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "ההורדה בוטלה";
            OverallProgressBar.Value = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה בהורדה: {ex.Message}";
            OverallProgressBar.Value = 0;
            MessageBox.Show($"שגיאה בהורדה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetDownloadingState(false);
        }
    }

    private async void DownloadAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentLessons == null || _currentLessons.Count == 0)
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
            StatusText.Text = $"מוריד {_currentLessons.Count} שיעורים...";
            OverallProgressBar.Value = 0;
            OverallProgressText.Text = "";

            await _downloadManager.DownloadAllAsync(_currentLessons, _downloadPath);

            var completed = _currentLessons.Count(l => l.Status == DownloadStatus.Completed);
            var skipped = _currentLessons.Count(l => l.Status == DownloadStatus.Skipped);
            var errors = _currentLessons.Count(l => l.Status == DownloadStatus.Error);

            StatusText.Text = $"הורדה הושלמה: {completed} הורדו, {skipped} כבר קיימים, {errors} שגיאות";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "ההורדה בוטלה";
            OverallProgressBar.Value = 0;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה בהורדה: {ex.Message}";
            OverallProgressBar.Value = 0;
            MessageBox.Show($"שגיאה בהורדה: {ex.Message}", "שגיאה", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetDownloadingState(false);
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e)
    {
        _downloadManager.Cancel();
        StatusText.Text = "מבטל הורדה...";
    }

    private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "בחר תיקייה להורדת שיעורים",
            InitialDirectory = _downloadPath
        };

        if (dialog.ShowDialog() == true)
        {
            _downloadPath = dialog.FolderName;
            DownloadPathText.Text = $"תיקיית הורדה: {_downloadPath}";

            // Re-check skip status for currently displayed lessons
            if (_currentLessons != null)
            {
                foreach (var lessonVm in _currentLessons)
                {
                    // Reset non-completed lessons and re-check
                    if (lessonVm.Status != DownloadStatus.Completed)
                    {
                        lessonVm.Status = DownloadStatus.Ready;
                        lessonVm.ProgressPercentage = 0;
                        lessonVm.StatusText = "מוכן";
                        lessonVm.CheckIfAlreadyDownloaded(_downloadPath);
                    }
                }
            }
        }
    }

    private void SetDownloadingState(bool isDownloading)
    {
        _isDownloading = isDownloading;
        CancelDownloadButton.IsEnabled = isDownloading;
        CancelDownloadButton.Visibility = isDownloading ? Visibility.Visible : Visibility.Collapsed;
        ChooseFolderButton.IsEnabled = !isDownloading;

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
        bool hasLessons = _currentLessons != null && _currentLessons.Count > 0;
        bool hasSeriesSelected = SeriesListBox.SelectedItem is Series && RabbiListBox.SelectedItem is Rabbi;

        DownloadSeriesButton.IsEnabled = hasSeriesSelected && !_isDownloading;
        DownloadAllButton.IsEnabled = hasLessons && !_isDownloading;
    }

    private void UpdateOverallProgress(int completed, int total)
    {
        if (total > 0)
        {
            OverallProgressBar.Value = (double)completed / total * 100;
            OverallProgressText.Text = $"הורדו {completed} מתוך {total} שיעורים";
        }
    }
}
