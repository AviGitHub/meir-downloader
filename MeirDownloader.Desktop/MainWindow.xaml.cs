using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
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
    private bool _isTopicMode;
    private List<LessonViewModel>? _currentLessons;
    private Topic? _selectedTopic;
    private readonly ObservableCollection<RabbiViewModel> _rabbiViewModels = new();
    private readonly ObservableCollection<Series> _seriesList = new();
    private readonly ObservableCollection<Topic> _topicViewModels = new();
    private readonly HashSet<string> _selectedRabbiFilterIds = new(); // empty = all selected

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

        UpdateThemeToggleButton();

        LoadRabbis();
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
        UpdateThemeToggleButton();
    }

    private void UpdateThemeToggleButton()
    {
        if (ThemeToggleButton != null)
        {
            ThemeToggleButton.Content = ThemeManager.CurrentTheme == AppTheme.Dark ? "☀" : "🌙";
            ThemeToggleButton.ToolTip = ThemeManager.CurrentTheme == AppTheme.Dark ? "Switch to Light Mode" : "Switch to Dark Mode";
        }
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

    // ── Tab switching ────────────────────────────────────────────────────────

    private void RabbisTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isTopicMode) return;
        _isTopicMode = false;

        // Swap button styles
        RabbisTabButton.Style = (Style)FindResource("ModernButtonStyle");
        TopicsTabButton.Style = (Style)FindResource("SecondaryButtonStyle");

        // Show rabbis, hide topics
        RabbiListBox.Visibility = Visibility.Visible;
        TopicsListBox.Visibility = Visibility.Collapsed;
        SearchBoxBorder.Visibility = Visibility.Visible;
        SeriesPanel.Visibility = Visibility.Visible;

        // Show series download button, hide topic download button
        DownloadSeriesButton.Visibility = Visibility.Visible;
        DownloadTopicButton.Visibility = Visibility.Collapsed;

        // Hide rabbi filter chips and rabbi column
        RabbiFilterPanel.Visibility = Visibility.Collapsed;
        RabbiNameColumn.Visibility = System.Windows.Visibility.Collapsed;

        // Clear topic selection
        _selectedTopic = null;
        TopicsListBox.SelectedItem = null;
        LessonsGrid.ItemsSource = null;
        _currentLessons = null;
        _selectedRabbiFilterIds.Clear();
        UpdateDownloadButtonStates();
    }

    private void TopicsTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isTopicMode) return;
        _isTopicMode = true;

        // Swap button styles
        TopicsTabButton.Style = (Style)FindResource("ModernButtonStyle");
        RabbisTabButton.Style = (Style)FindResource("SecondaryButtonStyle");

        // Show topics, hide rabbis
        RabbiListBox.Visibility = Visibility.Collapsed;
        TopicsListBox.Visibility = Visibility.Visible;
        SearchBoxBorder.Visibility = Visibility.Collapsed;
        SeriesPanel.Visibility = Visibility.Collapsed;

        // Show topic download button, hide series download button
        DownloadSeriesButton.Visibility = Visibility.Collapsed;
        DownloadTopicButton.Visibility = Visibility.Visible;

        // Show rabbi filter chips and rabbi column
        RabbiFilterPanel.Visibility = Visibility.Visible;
        RabbiNameColumn.Visibility = System.Windows.Visibility.Visible;
        PopulateRabbiFilterChips();

        // Clear rabbi/series selection
        RabbiListBox.SelectedItem = null;
        SeriesListBox.SelectedItem = null;
        _seriesList.Clear();
        LessonsGrid.ItemsSource = null;
        _currentLessons = null;
        UpdateDownloadButtonStates();

        // Load topics if not yet loaded
        if (_topicViewModels.Count == 0)
            LoadTopics();
    }

    private void PopulateRabbiFilterChips()
    {
        RabbiChipsPanel.Children.Clear();
        _selectedRabbiFilterIds.Clear();

        // "הכל" (All) chip — always first
        var allChip = CreateChip("הכל", null, isSelected: true);
        RabbiChipsPanel.Children.Add(allChip);

        foreach (var rabbiVm in _rabbiViewModels.OrderBy(r => r.Name))
        {
            var chip = CreateChip(rabbiVm.Name, rabbiVm.Id, isSelected: false);
            RabbiChipsPanel.Children.Add(chip);
        }
    }

    private System.Windows.Controls.Primitives.ToggleButton CreateChip(string label, string? rabbiId, bool isSelected)
    {
        var chip = new System.Windows.Controls.Primitives.ToggleButton
        {
            Content = label,
            IsChecked = isSelected,
            Tag = rabbiId,
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(10, 4, 10, 4),
            FontSize = 12,
            Cursor = System.Windows.Input.Cursors.Hand,
        };

        // Style the chip
        chip.Checked += RabbiChip_Changed;
        chip.Unchecked += RabbiChip_Changed;

        // Apply visual style inline
        chip.Style = TryFindResource("ChipToggleButtonStyle") as Style
                     ?? TryFindResource("SecondaryButtonStyle") as Style;

        return chip;
    }

    private void RabbiChip_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ToggleButton chip) return;

        var rabbiId = chip.Tag as string; // null = "All" chip

        if (rabbiId == null)
        {
            // "All" chip toggled
            if (chip.IsChecked == true)
            {
                // Deselect all individual chips
                _selectedRabbiFilterIds.Clear();
                foreach (System.Windows.Controls.Primitives.ToggleButton c in RabbiChipsPanel.Children)
                {
                    if (c.Tag != null) c.IsChecked = false;
                }
            }
        }
        else
        {
            if (chip.IsChecked == true)
            {
                _selectedRabbiFilterIds.Add(rabbiId);
                // Deselect "All" chip
                if (RabbiChipsPanel.Children.Count > 0 &&
                    RabbiChipsPanel.Children[0] is System.Windows.Controls.Primitives.ToggleButton allChip)
                    allChip.IsChecked = false;
            }
            else
            {
                _selectedRabbiFilterIds.Remove(rabbiId);
                // If nothing selected, re-select "All"
                if (_selectedRabbiFilterIds.Count == 0 &&
                    RabbiChipsPanel.Children.Count > 0 &&
                    RabbiChipsPanel.Children[0] is System.Windows.Controls.Primitives.ToggleButton allChip)
                    allChip.IsChecked = true;
            }
        }

        ApplyRabbiFilter();
    }

    private void ApplyRabbiFilter()
    {
        if (_currentLessons == null) return;

        if (_selectedRabbiFilterIds.Count == 0)
        {
            // Show all
            LessonsGrid.ItemsSource = _currentLessons;
        }
        else
        {
            // Filter by selected rabbi IDs — match by RabbiName since lessons store name not ID
            // Build name set from selected IDs
            var selectedNames = _rabbiViewModels
                .Where(r => _selectedRabbiFilterIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToHashSet();

            LessonsGrid.ItemsSource = _currentLessons
                .Where(l => selectedNames.Contains(l.RabbiName))
                .ToList();
        }
    }

    private async void LoadTopics()
    {
        try
        {
            _rabbiLoadingCts?.Cancel();
            _rabbiLoadingCts = new CancellationTokenSource();
            var ct = _rabbiLoadingCts.Token;

            RabbiLoadingBar.Visibility = Visibility.Visible;
            _topicViewModels.Clear();
            TopicsListBox.ItemsSource = _topicViewModels;
            StatusText.Text = "טוען נושאים...";

            await foreach (var page in _downloaderService.GetTopicsStreamAsync(ct))
            {
                foreach (var topic in page)
                    _topicViewModels.Add(topic);
                StatusText.Text = $"נטענו {_topicViewModels.Count} נושאים...";
            }

            StatusText.Text = $"נטענו {_topicViewModels.Count} נושאים";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה: {ex.Message}";
        }
        finally
        {
            RabbiLoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void TopicsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TopicsListBox.SelectedItem is not Topic topic) return;
        _selectedTopic = topic;

        try
        {
            _loadingCts?.Cancel();
            _loadingCts = new CancellationTokenSource();
            var ct = _loadingCts.Token;

            LessonsLoadingBar.Visibility = Visibility.Visible;
            StatusText.Text = $"טוען שיעורים עבור נושא: {topic.Name}...";
            LessonsGrid.ItemsSource = null;
            _currentLessons = null;
            UpdateDownloadButtonStates();

            var lessons = await _downloaderService.GetAllLessonsByTopicAsync(topic.Id, ct);

            _currentLessons = lessons
                .Select((lesson, index) => new LessonViewModel(lesson, index + 1))
                .ToList();

            var topicPath = Path.Combine(_downloadPath, "נושאים", SanitizeFileName(topic.Name));
            foreach (var lessonVm in _currentLessons)
                lessonVm.CheckIfAlreadyDownloaded(topicPath);

            ApplyRabbiFilter();
            StatusText.Text = $"נטענו {_currentLessons.Count} שיעורים עבור נושא: {topic.Name}";
            UpdateDownloadButtonStates();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה: {ex.Message}";
        }
        finally
        {
            LessonsLoadingBar.Visibility = Visibility.Collapsed;
        }
    }

    private async void DownloadTopicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTopic == null)
        {
            MessageBox.Show("אנא בחר נושא להורדה.", "בחירה חסרה", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_isDownloading)
        {
            MessageBox.Show("הורדה כבר מתבצעת. אנא המתן או בטל את ההורדה הנוכחית.", "הורדה פעילה",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var topic = _selectedTopic;

        try
        {
            SetDownloadingState(true);
            StatusText.Text = $"טוען שיעורים עבור נושא: {topic.Name}...";

            if (_currentLessons == null || _currentLessons.Count == 0)
            {
                _loadingCts?.Cancel();
                _loadingCts = new CancellationTokenSource();

                var lessons = await _downloaderService.GetAllLessonsByTopicAsync(topic.Id, _loadingCts.Token);

                _currentLessons = lessons
                    .Select((lesson, index) => new LessonViewModel(lesson, index + 1))
                    .ToList();

                LessonsGrid.ItemsSource = _currentLessons;
            }

            if (_currentLessons.Count == 0)
            {
                StatusText.Text = "לא נמצאו שיעורים בנושא זה";
                return;
            }

            // Download into נושאים/{topic name}/
            var topicDownloadPath = Path.Combine(_downloadPath, "נושאים", SanitizeFileName(topic.Name));

            StatusText.Text = $"מוריד {_currentLessons.Count} שיעורים מנושא: {topic.Name}";
            OverallProgressBar.Value = 0;
            OverallProgressText.Text = "";

            await _downloadManager.DownloadAllAsync(_currentLessons, topicDownloadPath);

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

                if (_seriesList.Count == 0)
                {
                    // Rabbi has no series — remove from list so it won't appear again
                    var vmToRemove = _rabbiViewModels.FirstOrDefault(r => r.Id == rabbi.Id);
                    if (vmToRemove != null)
                        _rabbiViewModels.Remove(vmToRemove);
                    RabbiListBox.SelectedItem = null;
                    StatusText.Text = $"לרב {rabbi.Name} אין סדרות — הוסר מהרשימה";
                }
                else
                {
                    StatusText.Text = $"נטענו {_seriesList.Count} סדרות עבור {rabbi.Name}";
                }
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
        _topicViewModels.Clear();
        _selectedTopic = null;
        RabbiListBox.ItemsSource = null;
        SeriesListBox.ItemsSource = null;
        TopicsListBox.ItemsSource = null;
        LessonsGrid.ItemsSource = null;
        _currentLessons = null;
        UpdateDownloadButtonStates();

        if (_isTopicMode)
            LoadTopics();
        else
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

                // Use topic subfolder when in topic mode
                var downloadPath = _isTopicMode && _selectedTopic != null
                    ? Path.Combine(_downloadPath, "נושאים", SanitizeFileName(_selectedTopic.Name))
                    : _downloadPath;

                var singleList = new List<LessonViewModel> { lessonVm };
                await _downloadManager.DownloadAllAsync(singleList, downloadPath);

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
            DownloadTopicButton.IsEnabled = false;
        }
    }

    private void UpdateDownloadButtonStates()
    {
        if (_isTopicMode)
        {
            DownloadTopicButton.IsEnabled = _selectedTopic != null && !_isDownloading;

            if (_selectedTopic != null)
            {
                var topicDir = Path.Combine(_downloadPath, "נושאים", SanitizeFileName(_selectedTopic.Name));
                var dirExists = Directory.Exists(topicDir);
                OpenDirectoryButton.Visibility = Visibility.Visible;
                OpenDirectoryButton.IsEnabled = dirExists;
                OpenDirectoryButton.ToolTip = dirExists ? "פתח את תיקיית ההורדה" : "התיקייה עדיין לא קיימת";
            }
            else
            {
                OpenDirectoryButton.Visibility = Visibility.Collapsed;
            }
            return;
        }

        bool hasSeriesSelected = SeriesListBox.SelectedItem is Series && RabbiListBox.SelectedItem is RabbiViewModel;
        DownloadSeriesButton.IsEnabled = hasSeriesSelected && !_isDownloading;

        // Show "Open Directory" button when a series is selected
        var selectedRabbi = RabbiListBox.SelectedItem as RabbiViewModel;
        var selectedSeries = SeriesListBox.SelectedItem as Series;
        if (selectedRabbi != null && selectedSeries != null)
        {
            var seriesDir = Path.Combine(
                _downloadPath,
                SanitizeFileName(selectedRabbi.Rabbi.Name),
                SanitizeFileName(selectedSeries.Name)
            );
            var dirExists = Directory.Exists(seriesDir);
            OpenDirectoryButton.Visibility = Visibility.Visible;
            OpenDirectoryButton.IsEnabled = dirExists;
            OpenDirectoryButton.ToolTip = dirExists
                ? "פתח את תיקיית ההורדה"
                : "התיקייה עדיין לא קיימת";
        }
        else
        {
            OpenDirectoryButton.Visibility = Visibility.Collapsed;
        }
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

        var bgBrush = FindResource("CardBackgroundBrush") as Brush ?? FindResource("SurfaceBrush") as Brush ?? Brushes.White;
        var textPrimary = FindResource("TextPrimaryBrush") as Brush ?? Brushes.Black;
        var textSecondary = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        var accentBrush = FindResource("AccentBrush") as Brush ?? Brushes.Teal;
        var borderBrush = FindResource("BorderBrush") as Brush ?? Brushes.LightGray;

        var aboutWindow = new Window
        {
            Title = "אודות",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize,
            FlowDirection = FlowDirection.RightToLeft,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            FontFamily = new FontFamily("Segoe UI"),
            Icon = this.Icon
        };

        // Outer border with rounded corners and theme background
        var outerBorder = new Border
        {
            Background = bgBrush,
            CornerRadius = new CornerRadius(12),
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 3,
                Opacity = 0.3,
                Color = Colors.Black
            }
        };

        var outerStack = new StackPanel();

        // Custom title bar
        var titleBar = new Grid
        {
            Background = Brushes.Transparent,
            Margin = new Thickness(0)
        };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = "אודות",
            FontSize = 13,
            Foreground = textSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(15, 10, 0, 10)
        };
        Grid.SetColumn(titleText, 0);
        titleBar.Children.Add(titleText);

        var closeX = new Button
        {
            Content = "✕",
            FontSize = 14,
            Foreground = textSecondary,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(12, 6, 12, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        closeX.Click += (_, _) => aboutWindow.Close();
        Grid.SetColumn(closeX, 1);
        titleBar.Children.Add(closeX);

        // Allow dragging the window by the title bar
        titleBar.MouseLeftButtonDown += (_, args) => { if (args.ClickCount == 1) aboutWindow.DragMove(); };

        outerStack.Children.Add(titleBar);

        // Separator line
        outerStack.Children.Add(new Border
        {
            Height = 1,
            Background = borderBrush,
            Margin = new Thickness(0)
        });

        // Content area
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(30, 20, 30, 30)
        };

        stack.Children.Add(new TextBlock
        {
            Text = "מוריד שיעורים",
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = textPrimary,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 5)
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Meir Downloader",
            FontSize = 14,
            Foreground = textSecondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Thickness(0, 0, 0, 10)
        });

        stack.Children.Add(new TextBlock
        {
            Text = $"גרסה {versionString}",
            FontSize = 13,
            Foreground = textSecondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 15)
        });

        var linkButton = new Button
        {
            Content = "GitHub: AviGitHub/meir-downloader",
            Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = accentBrush,
            Background = Brushes.Transparent,
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
            Width = 120,
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(15, 8, 15, 8),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand,
            Style = (Style)FindResource("SecondaryButtonStyle"),
        };
        closeButton.Click += (_, _) => aboutWindow.Close();
        stack.Children.Add(closeButton);

        outerStack.Children.Add(stack);
        outerBorder.Child = outerStack;
        aboutWindow.Content = outerBorder;
        aboutWindow.ShowDialog();
    }

    private void OpenDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? targetDir = null;

            if (_isTopicMode && _selectedTopic != null)
            {
                targetDir = Path.Combine(_downloadPath, "נושאים", SanitizeFileName(_selectedTopic.Name));
                if (!Directory.Exists(targetDir))
                    targetDir = Path.Combine(_downloadPath, "נושאים");
                if (!Directory.Exists(targetDir))
                    targetDir = _downloadPath;
            }
            else
            {
                var selectedRabbi = RabbiListBox.SelectedItem as RabbiViewModel;
                var selectedSeries = SeriesListBox.SelectedItem as Series;

                if (selectedRabbi == null || selectedSeries == null) return;

                var seriesDir = Path.Combine(
                    _downloadPath,
                    SanitizeFileName(selectedRabbi.Rabbi.Name),
                    SanitizeFileName(selectedSeries.Name)
                );

                if (Directory.Exists(seriesDir))
                    targetDir = seriesDir;
                else
                {
                    var rabbiDir = Path.Combine(_downloadPath, SanitizeFileName(selectedRabbi.Rabbi.Name));
                    targetDir = Directory.Exists(rabbiDir) ? rabbiDir : _downloadPath;
                }
            }

            if (targetDir != null && Directory.Exists(targetDir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetDir,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"שגיאה בפתיחת תיקייה: {ex.Message}";
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
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
