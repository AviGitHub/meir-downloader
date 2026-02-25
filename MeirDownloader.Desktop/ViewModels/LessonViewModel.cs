using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MeirDownloader.Core.Models;

namespace MeirDownloader.Desktop.ViewModels;

public enum DownloadStatus
{
    Ready,
    Downloading,
    Completed,
    Error,
    Skipped  // Already downloaded
}

public class LessonViewModel : INotifyPropertyChanged
{
    private DownloadStatus _status = DownloadStatus.Ready;
    private int _progressPercentage;
    private string _statusText = "מוכן";

    public Lesson Lesson { get; }
    public int LessonNumber { get; set; }

    public string Id => Lesson.Id;
    public string Title => Lesson.Title;
    public string RabbiName => Lesson.RabbiName;
    public string SeriesName => Lesson.SeriesName;
    public string AudioUrl => Lesson.AudioUrl;
    public string Date => Lesson.Date;
    public string FormattedDate
    {
        get
        {
            if (DateTime.TryParse(Lesson.Date, out var date))
                return date.ToString("dd.MM.yyyy");
            return Lesson.Date;
        }
    }
    public string DisplayTitle => $"{LessonNumber:D3} - {Title}";
    public string FormattedDuration
    {
        get
        {
            if (Lesson.Duration <= 0) return string.Empty;
            var ts = TimeSpan.FromSeconds(Lesson.Duration);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}"
                : $"0:{ts.Minutes:D2}";
        }
    }

    public DownloadStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDownloadable)); }
    }

    public int ProgressPercentage
    {
        get => _progressPercentage;
        set { _progressPercentage = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public bool IsDownloadable => Status == DownloadStatus.Ready || Status == DownloadStatus.Error;

    public LessonViewModel(Lesson lesson, int lessonNumber)
    {
        Lesson = lesson;
        LessonNumber = lessonNumber;
    }

    public string GetExpectedFilePath(string downloadPath)
    {
        var fileName = $"{LessonNumber:D3}-{SanitizeFileName(Title)}.mp3";
        bool hasRabbi = !string.IsNullOrWhiteSpace(RabbiName) && RabbiName != "Unknown";
        bool hasSeries = !string.IsNullOrWhiteSpace(SeriesName) && SeriesName != "Unknown";

        string targetDir;
        if (hasRabbi && hasSeries)
            targetDir = Path.Combine(downloadPath, SanitizeFileName(RabbiName), SanitizeFileName(SeriesName));
        else if (hasRabbi)
            targetDir = Path.Combine(downloadPath, SanitizeFileName(RabbiName));
        else if (hasSeries)
            targetDir = Path.Combine(downloadPath, SanitizeFileName(SeriesName));
        else
            targetDir = downloadPath;

        return Path.Combine(targetDir, fileName);
    }

    public void CheckIfAlreadyDownloaded(string downloadPath)
    {
        var filePath = GetExpectedFilePath(downloadPath);
        if (File.Exists(filePath))
        {
            Status = DownloadStatus.Skipped;
            ProgressPercentage = 100;
            StatusText = "הורד כבר ✅";
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
