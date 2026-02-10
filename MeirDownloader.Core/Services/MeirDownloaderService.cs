using MeirDownloader.Core.Models;
using System.Net;
using System.Text.Json;

namespace MeirDownloader.Core.Services;

public class MeirDownloaderService : IMeirDownloaderService
{
    private readonly HttpClient _httpClient;
    private const string BaseApiUrl = "https://meirtv.com/wp-json/wp/v2";
    private const string AudioBaseUrl = "https://mp3.meirtv.co.il//wp2";

    public MeirDownloaderService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MeirDownloader/2.0");
    }

    public async Task<List<Rabbi>> GetRabbisAsync(CancellationToken ct = default)
    {
        var allRabbis = new List<Rabbi>();

        try
        {
            int page = 1;
            int totalPages = 1;

            while (page <= totalPages)
            {
                var url = $"{BaseApiUrl}/rabbis?per_page=100&page={page}&orderby=count&order=desc&_fields=id,name,slug,count";
                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch rabbis page {page}: HTTP {(int)response.StatusCode}");
                    break;
                }

                if (page == 1 && response.Headers.TryGetValues("X-WP-TotalPages", out var totalPagesValues))
                {
                    int.TryParse(totalPagesValues.FirstOrDefault(), out totalPages);
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var items = JsonSerializer.Deserialize<JsonElement>(json);

                if (items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        allRabbis.Add(new Rabbi
                        {
                            Id = item.GetProperty("id").GetInt32().ToString(),
                            Name = item.GetProperty("name").GetString() ?? string.Empty,
                            Count = item.GetProperty("count").GetInt32()
                        });
                    }
                }

                page++;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching rabbis: {ex.Message}");
        }

        return allRabbis;
    }

    public async Task<List<Series>> GetSeriesAsync(string? rabbiId = null, CancellationToken ct = default)
    {
        var allSeries = new List<Series>();

        try
        {
            int page = 1;
            int totalPages = 1;

            while (page <= totalPages)
            {
                var url = $"{BaseApiUrl}/shiurim-series?per_page=100&page={page}&orderby=count&order=desc&_fields=id,name,slug,count";
                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch series page {page}: HTTP {(int)response.StatusCode}");
                    break;
                }

                if (page == 1 && response.Headers.TryGetValues("X-WP-TotalPages", out var totalPagesValues))
                {
                    int.TryParse(totalPagesValues.FirstOrDefault(), out totalPages);
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var items = JsonSerializer.Deserialize<JsonElement>(json);

                if (items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        allSeries.Add(new Series
                        {
                            Id = item.GetProperty("id").GetInt32().ToString(),
                            Name = WebUtility.HtmlDecode(item.GetProperty("name").GetString() ?? string.Empty),
                            Count = item.GetProperty("count").GetInt32()
                        });
                    }
                }

                page++;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching series: {ex.Message}");
        }

        return allSeries;
    }

    public async Task<List<Lesson>> GetLessonsAsync(string? rabbiId = null, string? seriesId = null, int page = 1, CancellationToken ct = default)
    {
        var lessons = new List<Lesson>();

        try
        {
            var url = $"{BaseApiUrl}/shiurim?per_page=20&page={page}&_fields=id,title,date,rabbis,shiurim-series,link";

            if (!string.IsNullOrEmpty(rabbiId))
                url += $"&rabbis={rabbiId}";

            if (!string.IsNullOrEmpty(seriesId))
                url += $"&shiurim-series={seriesId}";

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch lessons page {page}: HTTP {(int)response.StatusCode}");
                return lessons;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<JsonElement>(json);

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();
                    var titleRendered = item.GetProperty("title").GetProperty("rendered").GetString() ?? string.Empty;

                    lessons.Add(new Lesson
                    {
                        Id = id.ToString(),
                        Title = WebUtility.HtmlDecode(titleRendered),
                        RabbiName = "Unknown",
                        SeriesName = "Unknown",
                        AudioUrl = $"{AudioBaseUrl}/{id}.mp3",
                        Date = item.GetProperty("date").GetString() ?? string.Empty,
                        Duration = 0
                    });
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching lessons: {ex.Message}");
        }

        return lessons;
    }

    public async Task<List<Lesson>> GetAllLessonsAsync(string? rabbiId = null, string? seriesId = null, CancellationToken ct = default)
    {
        var allLessons = new List<Lesson>();

        try
        {
            int page = 1;
            int totalPages = 1;

            while (page <= totalPages)
            {
                ct.ThrowIfCancellationRequested();

                var url = $"{BaseApiUrl}/shiurim?per_page=100&page={page}&_fields=id,title,date,rabbis,shiurim-series,link";

                if (!string.IsNullOrEmpty(rabbiId))
                    url += $"&rabbis={rabbiId}";

                if (!string.IsNullOrEmpty(seriesId))
                    url += $"&shiurim-series={seriesId}";

                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch lessons page {page}: HTTP {(int)response.StatusCode}");
                    break;
                }

                if (page == 1 && response.Headers.TryGetValues("X-WP-TotalPages", out var totalPagesValues))
                {
                    int.TryParse(totalPagesValues.FirstOrDefault(), out totalPages);
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var items = JsonSerializer.Deserialize<JsonElement>(json);

                if (items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var id = item.GetProperty("id").GetInt32();
                        var titleRendered = item.GetProperty("title").GetProperty("rendered").GetString() ?? string.Empty;

                        allLessons.Add(new Lesson
                        {
                            Id = id.ToString(),
                            Title = WebUtility.HtmlDecode(titleRendered),
                            RabbiName = "Unknown",
                            SeriesName = "Unknown",
                            AudioUrl = $"{AudioBaseUrl}/{id}.mp3",
                            Date = item.GetProperty("date").GetString() ?? string.Empty,
                            Duration = 0
                        });
                    }
                }

                page++;
            }

            // Sort by date ascending (oldest first) so numbering makes chronological sense
            allLessons = allLessons
                .OrderBy(l => l.Date)
                .ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching all lessons: {ex.Message}");
        }

        return allLessons;
    }

    public async Task<string> DownloadLessonAsync(Lesson lesson, string downloadPath, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        return await DownloadLessonInternalAsync(lesson, downloadPath, null, progress, ct);
    }

    public async Task<string> DownloadLessonAsync(Lesson lesson, string downloadPath, int index, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        return await DownloadLessonInternalAsync(lesson, downloadPath, index, progress, ct);
    }

    private async Task<string> DownloadLessonInternalAsync(Lesson lesson, string downloadPath, int? index, IProgress<DownloadProgress> progress, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(lesson.AudioUrl))
            throw new ArgumentException("Lesson audio URL is empty");

        try
        {
            var rabbiDir = Path.Combine(downloadPath, SanitizeFileName(lesson.RabbiName));
            var seriesDir = Path.Combine(rabbiDir, SanitizeFileName(lesson.SeriesName));
            Directory.CreateDirectory(seriesDir);

            string fileName;
            if (index.HasValue)
            {
                fileName = $"{index.Value:D3}-{SanitizeFileName(lesson.Title)}.mp3";
            }
            else
            {
                fileName = $"{SanitizeFileName(lesson.Title)}.mp3";
            }

            var filePath = Path.Combine(seriesDir, fileName);

            using var response = await _httpClient.GetAsync(lesson.AudioUrl, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Failed to download audio: HTTP {(int)response.StatusCode}");

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long bytesRead = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, ct)) != 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;

                progress.Report(new DownloadProgress
                {
                    LessonId = lesson.Id,
                    LessonTitle = lesson.Title,
                    BytesDownloaded = bytesRead,
                    TotalBytes = totalBytes,
                    IsComplete = totalBytes > 0 && bytesRead >= totalBytes
                });
            }

            return filePath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to download lesson: {ex.Message}", ex);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }
}
