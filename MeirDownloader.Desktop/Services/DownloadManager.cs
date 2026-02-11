using System.IO;
using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;
using MeirDownloader.Desktop.ViewModels;

namespace MeirDownloader.Desktop.Services;

public class DownloadManager
{
    private readonly IMeirDownloaderService _service;
    private readonly SemaphoreSlim _semaphore;
    private CancellationTokenSource? _cts;

    public int MaxConcurrentDownloads { get; }
    public bool IsDownloading => _cts != null && !_cts.IsCancellationRequested;

    public event Action<int, int>? OverallProgressChanged; // completed, total

    public DownloadManager(IMeirDownloaderService service, int maxConcurrent = 4)
    {
        _service = service;
        MaxConcurrentDownloads = maxConcurrent;
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);
    }

    public async Task DownloadAllAsync(IList<LessonViewModel> lessons, string downloadPath)
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        int completed = 0;
        int total = lessons.Count;

        // Mark already-downloaded lessons
        foreach (var lesson in lessons)
        {
            lesson.CheckIfAlreadyDownloaded(downloadPath);
        }

        // Count skipped as already completed
        int skipped = lessons.Count(l => l.Status == DownloadStatus.Skipped);
        completed = skipped;
        OverallProgressChanged?.Invoke(completed, total);

        // Download remaining in parallel
        var downloadTasks = lessons
            .Where(l => l.Status != DownloadStatus.Skipped)
            .Select(lesson => DownloadSingleAsync(lesson, downloadPath, ct, () =>
            {
                Interlocked.Increment(ref completed);
                OverallProgressChanged?.Invoke(completed, total);
            }))
            .ToList();

        try
        {
            await Task.WhenAll(downloadTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }
        finally
        {
            _cts = null;
        }
    }

    private async Task DownloadSingleAsync(LessonViewModel lessonVm, string downloadPath, CancellationToken ct, Action onComplete)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            ct.ThrowIfCancellationRequested();

            lessonVm.Status = DownloadStatus.Downloading;
            lessonVm.StatusText = "מוריד...";
            lessonVm.ProgressPercentage = 0;

            var progress = new Progress<DownloadProgress>(p =>
            {
                lessonVm.ProgressPercentage = p.ProgressPercentage;
                lessonVm.StatusText = $"{p.ProgressPercentage}%";
            });

            // Create directory structure
            var rabbiDir = Path.Combine(downloadPath, SanitizeFileName(lessonVm.RabbiName));
            var seriesDir = Path.Combine(rabbiDir, SanitizeFileName(lessonVm.SeriesName));
            Directory.CreateDirectory(seriesDir);

            var fileName = $"{lessonVm.LessonNumber:D3}-{SanitizeFileName(lessonVm.Title)}.mp3";
            var filePath = Path.Combine(seriesDir, fileName);

            // Use the service's download method with lesson number for proper file naming
            await _service.DownloadLessonAsync(lessonVm.Lesson, downloadPath, lessonVm.LessonNumber, progress, ct);

            lessonVm.Status = DownloadStatus.Completed;
            lessonVm.ProgressPercentage = 100;
            lessonVm.StatusText = "הושלם ✅";
            onComplete();
        }
        catch (OperationCanceledException)
        {
            lessonVm.Status = DownloadStatus.Ready;
            lessonVm.StatusText = "בוטל";
            lessonVm.ProgressPercentage = 0;
        }
        catch (Exception ex)
        {
            lessonVm.Status = DownloadStatus.Error;
            lessonVm.StatusText = $"שגיאה ❌";
            System.Diagnostics.Debug.WriteLine($"Download error for {lessonVm.Title}: {ex.Message}");
            onComplete(); // Count errors as completed for progress
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }
}
