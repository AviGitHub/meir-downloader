using MeirDownloader.Core.Models;

namespace MeirDownloader.Core.Services;

public interface IMeirDownloaderService
{
    Task<List<Rabbi>> GetRabbisAsync(CancellationToken ct = default);
    Task<List<Series>> GetSeriesAsync(string? rabbiId = null, CancellationToken ct = default);
    Task<List<Lesson>> GetLessonsAsync(string? rabbiId = null, string? seriesId = null, int page = 1, CancellationToken ct = default);
    Task<List<Lesson>> GetAllLessonsAsync(string? rabbiId = null, string? seriesId = null, CancellationToken ct = default);
    Task<string> DownloadLessonAsync(Lesson lesson, string downloadPath, IProgress<DownloadProgress> progress, CancellationToken ct = default);
    Task<string> DownloadLessonAsync(Lesson lesson, string downloadPath, int index, IProgress<DownloadProgress> progress, CancellationToken ct = default);
}
