using System.IO;
using System.Net.Http;
using MeirDownloader.Core.Models;
using MeirDownloader.Core.Services;
using MeirDownloader.Desktop.ViewModels;

namespace MeirDownloader.Desktop.Services;

public class DownloadManager
{
    private readonly IMeirDownloaderService _service;
    private readonly SemaphoreSlim _semaphore;
    private CancellationTokenSource? _cts;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(60) };

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

    private static void Log(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[MeirDownloader] {message}");
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MeirDownloader", "download.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch { }
    }

    private async Task DownloadSingleAsync(LessonViewModel lessonVm, string downloadPath, CancellationToken ct, Action onComplete)
    {
        await _semaphore.WaitAsync(ct);
        string? filePath = null;
        try
        {
            ct.ThrowIfCancellationRequested();

            lessonVm.Status = DownloadStatus.Downloading;
            lessonVm.StatusText = "מאתר קובץ...";
            lessonVm.ProgressPercentage = 0;

            // Resolve the real audio URL from the lesson webpage
            var resolvedUrl = await _service.ResolveAudioUrlAsync(lessonVm.Lesson.Link, ct);
            if (!string.IsNullOrEmpty(resolvedUrl))
            {
                Log($"Resolved URL for '{lessonVm.Title}': {resolvedUrl}");
                lessonVm.Lesson.AudioUrl = resolvedUrl;
            }
            else
            {
                Log($"URL resolution failed for '{lessonVm.Title}', falling back to wp2 pattern: {lessonVm.Lesson.AudioUrl}");
            }

            lessonVm.StatusText = "מוריד...";

            // Create directory structure
            var rabbiDir = Path.Combine(downloadPath, SanitizeFileName(lessonVm.RabbiName));
            var seriesDir = Path.Combine(rabbiDir, SanitizeFileName(lessonVm.SeriesName));
            Directory.CreateDirectory(seriesDir);

            var fileName = $"{lessonVm.LessonNumber:D3}-{SanitizeFileName(lessonVm.Title)}.mp3";
            filePath = Path.Combine(seriesDir, fileName);

            Log($"Starting download for '{lessonVm.Title}' to {filePath}");

            using var response = await _httpClient.GetAsync(lessonVm.Lesson.AudioUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = 0;
            int bytesRead;
            var lastReportTime = DateTime.MinValue;

            // Capture context for progress updates
            var progressReporter = new Progress<double>(p =>
            {
                lessonVm.ProgressPercentage = (int)p;
                lessonVm.StatusText = $"{(int)p}%";
            });
            var progress = (IProgress<double>)progressReporter;

            while ((bytesRead = await contentStream.ReadAsync(buffer, ct)) != 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                totalRead += bytesRead;

                var now = DateTime.UtcNow;
                if (totalBytes > 0 && (now - lastReportTime).TotalMilliseconds > 200) // Throttle to 200ms
                {
                    var percentage = (double)totalRead * 100 / totalBytes;
                    progress.Report(percentage);
                    lastReportTime = now;
                }
            }

            lessonVm.Status = DownloadStatus.Completed;
            lessonVm.ProgressPercentage = 100;
            lessonVm.StatusText = "הושלם ✅";
            Log($"Download completed for '{lessonVm.Title}'");
            onComplete();
        }
        catch (OperationCanceledException)
        {
            lessonVm.Status = DownloadStatus.Ready;
            lessonVm.StatusText = "בוטל";
            lessonVm.ProgressPercentage = 0;
            Log($"Download cancelled for '{lessonVm.Title}'");
            CleanupPartialFile(filePath);
        }
        catch (Exception ex)
        {
            lessonVm.Status = DownloadStatus.Error;
            var shortError = ex.Message.Length > 50 ? ex.Message[..50] + "..." : ex.Message;
            lessonVm.StatusText = $"שגיאה: {shortError}";
            Log($"Download error for '{lessonVm.Title}': {ex.Message}");
            CleanupPartialFile(filePath);
            onComplete(); // Count errors as completed for progress
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static void CleanupPartialFile(string? filePath)
    {
        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                Log($"Deleted partial file: {filePath}");
            }
            catch (Exception ex)
            {
                Log($"Failed to delete partial file {filePath}: {ex.Message}");
            }
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
