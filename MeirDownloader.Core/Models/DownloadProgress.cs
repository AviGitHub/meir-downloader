namespace MeirDownloader.Core.Models;

public class DownloadProgress
{
    public string LessonId { get; set; } = string.Empty;
    public string LessonTitle { get; set; } = string.Empty;
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public bool IsComplete { get; set; }
    public string? Error { get; set; }

    public int ProgressPercentage => TotalBytes > 0 ? (int)(BytesDownloaded * 100 / TotalBytes) : 0;
}
