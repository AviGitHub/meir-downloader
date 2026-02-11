using System.ComponentModel;
using System.IO;
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
    public string DisplayTitle => $"{LessonNumber:D3} - {Title}";

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
        var rabbiDir = SanitizeFileName(RabbiName);
        var seriesDir = SanitizeFileName(SeriesName);
        var fileName = $"{LessonNumber:D3}-{SanitizeFileName(Title)}.mp3";
        return Path.Combine(downloadPath, rabbiDir, seriesDir, fileName);
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
