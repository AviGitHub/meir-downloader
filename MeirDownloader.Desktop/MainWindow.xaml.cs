using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;
using MeirDownloader.Desktop.Services;
using MeirDownloader.Desktop.ViewModels;

namespace MeirDownloader.Desktop;

public partial class MainWindow : Window
{
    private readonly IMeirDownloaderService _downloaderService;
    private readonly DownloadManager _downloadManager;
    private readonly ImageCacheService _imageCacheService = new();
    private string _downloadPath;
    private CancellationTokenSource? _loadingCts;
    private CancellationTokenSource? _rabbiLoadingCts;
    private CancellationTokenSource? _seriesLoadingCts;
    private CancellationTokenSource? _imageLoadingCts;
    private bool _isDownloading;
    private List<LessonViewModel>? _currentLessons;
    private readonly ObservableCollection<RabbiViewModel> _rabbiViewModels = new();
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

        // Set version display dynamically from assembly info
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = $"v{version?.Major}.{version?.Minor}.{version?.Build}";

        LoadRabbis();
    }

    private async void LoadRabbis()
    {
        try
        {
            _rabbiLoadingCts?.Cancel();
            _imageLoadingCts?.Cancel();
            _rabbiLoadingCts = new CancellationTokenSource();
            _imageLoadingCts = new CancellationTokenSource();
            var ct = _rabbiLoadingCts.Token;

            RabbiLoadingBar.Visibility = Visibility.Visible;
            _rabbiViewModels.Clear();
            RabbiListBox.ItemsSource = _rabbiViewModels;
            StatusText.Text = "טוען רבנים...";

            await foreach (var page in _downloaderService.GetRabbisStreamAsync(ct))
            {
                foreach (var rabbi in page.Where(r => r.Count > 0))
                {
                    _rabbiViewModels.Add(new RabbiViewModel(rabbi));
                }
                StatusText.Text = $"נטענו {_rabbiViewModels.Count} רבנים...";
            }

            StatusText.Text = $"נטענו {_rabbiViewModels.Count} רבנים";

            // Fire-and-forget image loading
            _ = LoadRabbiImagesAsync(_imageLoadingCts.Token);
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

    private async Task LoadRabbiImagesAsync(CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(3);
        var tasks = _rabbiViewModels.ToList().Select(async rabbiVm =>
        {
            if (rabbiVm.ImageLoaded) return;
            
            await Application.Current.Dispatcher.InvokeAsync(() => rabbiVm.IsImageLoading = true);
            
            await semaphore.WaitAsync(ct);
            try
            {
                if (string.IsNullOrEmpty(rabbiVm.Rabbi.ImageUrl))
                {
                    var imageUrl = await _downloaderService.GetRabbiImageUrlAsync(rabbiVm.Link, ct);
                    rabbiVm.Rabbi.ImageUrl = imageUrl;
                }

                if (!string.IsNullOrEmpty(rabbiVm.Rabbi.ImageUrl))
                {
                    var image = await _imageCacheService.GetImageAsync(rabbiVm.Rabbi.ImageUrl, rabbiVm.Id, ct);
                    if (image != null)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            rabbiVm.AvatarImage = image;
                        });
                    }
                }
                rabbiVm.MarkImageLoaded();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading image for {rabbiVm.Name}: {ex.Message}");
                rabbiVm.MarkImageLoaded();
            }
            finally
            {
                await Application.Current.Dispatcher.InvokeAsync(() => rabbiVm.IsImageLoading = false);
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private async void RabbiListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RabbiListBox.SelectedItem is RabbiViewModel rabbiVm)
        {
            var rabbi = rabbiVm.Rabbi;
            try
            {
                _seriesLoadingCts?.Cancel();
                _seriesLoadingCts = new CancellationTokenSource();
                var ct = _seriesLoadingCts.Token;

                SeriesLoadingBar.Visibility = Visibility.Visible;
                StatusText.Text = $"סורק שיעורים עבור {rabbi.Name}...";
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
                    StatusText.Text = $"סורק שיעורים עבור {rabbi.Name}... נמצאו {_seriesList.Count} סדרות";
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
        if (SeriesListBox.SelectedItem is Series series && RabbiListBox.SelectedItem is RabbiViewModel rabbiVm)
        {
            var rabbi = rabbiVm.Rabbi;
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

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            var view = CollectionViewSource.GetDefaultView(_rabbiViewModels);
            if (view != null)
            {
                view.Filter = o =>
                {
                    if (string.IsNullOrWhiteSpace(textBox.Text)) return true;
                    if (o is RabbiViewModel rabbiVm)
                    {
                        return rabbiVm.Name.Contains(textBox.Text, StringComparison.OrdinalIgnoreCase);
                    }
                    return false;
                };
            }
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _rabbiLoadingCts?.Cancel();
        _seriesLoadingCts?.Cancel();
        _imageLoadingCts?.Cancel();
        _rabbiViewModels.Clear();
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
        if (RabbiListBox.SelectedItem is not RabbiViewModel rabbiVm || SeriesListBox.SelectedItem is not Series series)
        {
            MessageBox.Show("אנא בחר רב וסדרה להורדה.", "בחירה חסרה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var rabbi = rabbiVm.Rabbi;

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
        bool hasSeriesSelected = SeriesListBox.SelectedItem is Series && RabbiListBox.SelectedItem is RabbiViewModel;

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

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        var versionString = $"{version?.Major}.{version?.Minor}.{version?.Build}";

        var aboutWindow = new Window
        {
            Title = "אודות",
            Width = 400,
            Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            FlowDirection = FlowDirection.RightToLeft,
            Background = System.Windows.Media.Brushes.White,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
            Icon = this.Icon
        };

        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(30)
        };

        stack.Children.Add(new TextBlock
        {
            Text = "מוריד שיעורים",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Meir Downloader",
            FontSize = 14,
            Foreground = System.Windows.Media.Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Thickness(0, 0, 0, 10)
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"גרסה {versionString}",
            FontSize = 13,
            Foreground = System.Windows.Media.Brushes.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15)
        });

        var linkButton = new Button
        {
            Content = "GitHub: AviGitHub/meir-downloader",
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1ABC9C")),
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 13,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Thickness(5, 3, 5, 3),
            Margin = new Thickness(0, 0, 0, 20)
        };
        linkButton.Click += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/AviGitHub/meir-downloader",
                UseShellExecute = true
            });
        };
        stack.Children.Add(linkButton);

        var closeButton = new Button
        {
            Content = "סגור",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(10, 5, 10, 5)
        };
        closeButton.Click += (_, _) => aboutWindow.Close();
        stack.Children.Add(closeButton);

        aboutWindow.Content = stack;
        aboutWindow.ShowDialog();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_downloaderService is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
