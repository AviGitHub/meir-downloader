using HtmlAgilityPack;
using MeirDownloader.Core.Models;
using System.Net;
using System.Text.RegularExpressions;

namespace MeirDownloader.Core.Services;

public class MeirDownloaderService : IMeirDownloaderService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://meirtv.com";
    private const string ApiEndpoint = $"{BaseUrl}/wp-admin/admin-ajax.php";

    public MeirDownloaderService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<List<Rabbi>> GetRabbisAsync(CancellationToken ct = default)
    {
        try
        {
            var data = new Dictionary<string, string>
            {
                { "action", "wpgb_get_posts" },
                { "grid", "1" },
                { "paged", "1" }
            };

            var content = new FormUrlEncodedContent(data);
            var response = await _httpClient.PostAsync(ApiEndpoint, content, ct);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync(ct);
            var rabbis = ParseRabbisFromJson(jsonString);
            return rabbis;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch rabbis: {ex.Message}", ex);
        }
    }

    public async Task<List<Series>> GetSeriesAsync(string? rabbiId = null, CancellationToken ct = default)
    {
        try
        {
            var data = new Dictionary<string, string>
            {
                { "action", "wpgb_get_posts" },
                { "grid", "1" },
                { "paged", "1" }
            };

            if (!string.IsNullOrEmpty(rabbiId))
            {
                data["facets[rabbis]"] = rabbiId;
            }

            var content = new FormUrlEncodedContent(data);
            var response = await _httpClient.PostAsync(ApiEndpoint, content, ct);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync(ct);
            var series = ParseSeriesFromJson(jsonString);
            return series;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch series: {ex.Message}", ex);
        }
    }

    public async Task<List<Lesson>> GetLessonsAsync(string? rabbiId = null, string? seriesId = null, int page = 1, CancellationToken ct = default)
    {
        try
        {
            var data = new Dictionary<string, string>
            {
                { "action", "wpgb_get_posts" },
                { "grid", "1" },
                { "paged", page.ToString() }
            };

            if (!string.IsNullOrEmpty(rabbiId))
                data["facets[rabbis]"] = rabbiId;

            if (!string.IsNullOrEmpty(seriesId))
                data["facets[shiurim-series]"] = seriesId;

            var content = new FormUrlEncodedContent(data);
            var response = await _httpClient.PostAsync(ApiEndpoint, content, ct);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync(ct);
            var lessons = ParseLessonsFromJson(jsonString);
            return lessons;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch lessons: {ex.Message}", ex);
        }
    }

    public async Task<string> DownloadLessonAsync(Lesson lesson, string downloadPath, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(lesson.AudioUrl))
            throw new ArgumentException("Lesson audio URL is empty");

        try
        {
            var rabbiDir = Path.Combine(downloadPath, SanitizeFileName(lesson.RabbiName));
            var seriesDir = Path.Combine(rabbiDir, SanitizeFileName(lesson.SeriesName));
            Directory.CreateDirectory(seriesDir);

            var fileName = $"{SanitizeFileName(lesson.Title)}.mp3";
            var filePath = Path.Combine(seriesDir, fileName);

            using (var response = await _httpClient.GetAsync(lesson.AudioUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using (var contentStream = await response.Content.ReadAsStreamAsync(ct))
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long bytesRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) != 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, ct);
                        bytesRead += read;

                        progress.Report(new DownloadProgress
                        {
                            LessonId = lesson.Id,
                            LessonTitle = lesson.Title,
                            BytesDownloaded = bytesRead,
                            TotalBytes = totalBytes,
                            IsComplete = bytesRead == totalBytes
                        });
                    }
                }
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

    private List<Rabbi> ParseRabbisFromJson(string json)
    {
        var rabbis = new List<Rabbi>();
        try
        {
            var matches = Regex.Matches(json, @"value=""([^""]+)""[^>]*>([^<]+)\((\d+)\)");
            foreach (Match match in matches)
            {
                if (match.Groups[1].Value != "")
                {
                    rabbis.Add(new Rabbi
                    {
                        Id = match.Groups[1].Value,
                        Name = match.Groups[2].Value.Trim(),
                        Count = int.Parse(match.Groups[3].Value)
                    });
                }
            }
        }
        catch { }
        return rabbis;
    }

    private List<Series> ParseSeriesFromJson(string json)
    {
        var series = new List<Series>();
        try
        {
            var matches = Regex.Matches(json, @"value=""([^""]+)""[^>]*>([^<]+)\s*\(&nbsp;?(\d+)");
            foreach (Match match in matches)
            {
                if (match.Groups[1].Value != "")
                {
                    series.Add(new Series
                    {
                        Id = match.Groups[1].Value,
                        Name = WebUtility.HtmlDecode(match.Groups[2].Value.Trim()),
                        Count = int.Parse(match.Groups[3].Value)
                    });
                }
            }
        }
        catch { }
        return series;
    }

    private List<Lesson> ParseLessonsFromJson(string json)
    {
        var lessons = new List<Lesson>();
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(json);

            // Extract lessons from HTML posts
            var postMatches = Regex.Matches(json, @"<a[^>]*href=""https://meirtv\.com/shiurim/([^/""]+)/""[^>]*>([^<]+)</a>");
            foreach (Match match in postMatches)
            {
                lessons.Add(new Lesson
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = WebUtility.HtmlDecode(match.Groups[2].Value.Trim()),
                    RabbiName = "Unknown",
                    SeriesName = "Unknown",
                    AudioUrl = "",
                    Date = DateTime.Now.ToString("yyyy-MM-dd"),
                    Duration = 0
                });
            }
        }
        catch { }
        return lessons;
    }

    private string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName
            .Where(c => !invalidChars.Contains(c))
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Unknown" : sanitized;
    }
}
