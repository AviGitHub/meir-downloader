using MeirDownloader.Core.Models;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        if (!string.IsNullOrEmpty(rabbiId))
        {
            return await GetSeriesForRabbiAsync(rabbiId, ct);
        }

        return await GetAllSeriesAsync(ct);
    }

    /// <summary>
    /// Two-step approach: fetch all lessons for a rabbi to discover series IDs,
    /// then fetch series details for those IDs.
    /// </summary>
    private async Task<List<Series>> GetSeriesForRabbiAsync(string rabbiId, CancellationToken ct)
    {
        var seriesLessonCount = new Dictionary<int, int>();

        try
        {
            // Step 1: Paginate through all lessons for this rabbi to collect series IDs and counts
            int page = 1;
            int totalPages = 1;

            while (page <= totalPages)
            {
                ct.ThrowIfCancellationRequested();

                var url = $"{BaseApiUrl}/shiurim?rabbis={rabbiId}&per_page=100&page={page}&_fields=shiurim-series";
                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch lessons for rabbi {rabbiId} page {page}: HTTP {(int)response.StatusCode}");
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
                        if (item.TryGetProperty("shiurim-series", out var seriesArray) && seriesArray.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var seriesId in seriesArray.EnumerateArray())
                            {
                                var id = seriesId.GetInt32();
                                if (seriesLessonCount.ContainsKey(id))
                                    seriesLessonCount[id]++;
                                else
                                    seriesLessonCount[id] = 1;
                            }
                        }
                    }
                }

                page++;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching lessons for rabbi {rabbiId}: {ex.Message}");
        }

        if (seriesLessonCount.Count == 0)
            return new List<Series>();

        // Step 2: Fetch series details for the discovered IDs (in chunks of 100)
        var allSeries = new List<Series>();
        var seriesIds = seriesLessonCount.Keys.ToList();

        try
        {
            for (int i = 0; i < seriesIds.Count; i += 100)
            {
                ct.ThrowIfCancellationRequested();

                var chunk = seriesIds.Skip(i).Take(100);
                var includeParam = string.Join(",", chunk);
                var url = $"{BaseApiUrl}/shiurim-series?include={includeParam}&per_page=100&_fields=id,name,count";
                var response = await _httpClient.GetAsync(url, ct);

                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to fetch series details: HTTP {(int)response.StatusCode}");
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var items = JsonSerializer.Deserialize<JsonElement>(json);

                if (items.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        var id = item.GetProperty("id").GetInt32();
                        var rabbiSpecificCount = seriesLessonCount.ContainsKey(id) ? seriesLessonCount[id] : 0;

                        if (rabbiSpecificCount > 0)
                        {
                            allSeries.Add(new Series
                            {
                                Id = id.ToString(),
                                Name = WebUtility.HtmlDecode(item.GetProperty("name").GetString() ?? string.Empty),
                                Count = rabbiSpecificCount
                            });
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error fetching series details: {ex.Message}");
        }

        // Sort by rabbi-specific lesson count descending
        return allSeries.OrderByDescending(s => s.Count).ToList();
    }

    /// <summary>
    /// Fetch all series with hide_empty=true to exclude 0-count series.
    /// </summary>
    private async Task<List<Series>> GetAllSeriesAsync(CancellationToken ct)
    {
        var allSeries = new List<Series>();

        try
        {
            int page = 1;
            int totalPages = 1;

            while (page <= totalPages)
            {
                var url = $"{BaseApiUrl}/shiurim-series?per_page=100&page={page}&hide_empty=true&orderby=count&order=desc&_fields=id,name,count";
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
                    var link = item.TryGetProperty("link", out var linkProp) ? linkProp.GetString() ?? string.Empty : string.Empty;

                    lessons.Add(new Lesson
                    {
                        Id = id.ToString(),
                        Title = WebUtility.HtmlDecode(titleRendered),
                        RabbiName = "Unknown",
                        SeriesName = "Unknown",
                        AudioUrl = $"{AudioBaseUrl}/{id}.mp3",
                        Link = link,
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
                        var link = item.TryGetProperty("link", out var linkProp) ? linkProp.GetString() ?? string.Empty : string.Empty;

                        allLessons.Add(new Lesson
                        {
                            Id = id.ToString(),
                            Title = WebUtility.HtmlDecode(titleRendered),
                            RabbiName = "Unknown",
                            SeriesName = "Unknown",
                            AudioUrl = $"{AudioBaseUrl}/{id}.mp3",
                            Link = link,
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

    public async IAsyncEnumerable<List<Rabbi>> GetRabbisStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        int page = 1;
        int totalPages = 1;

        while (page <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

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
            var pageResults = new List<Rabbi>();

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    pageResults.Add(new Rabbi
                    {
                        Id = item.GetProperty("id").GetInt32().ToString(),
                        Name = item.GetProperty("name").GetString() ?? string.Empty,
                        Count = item.GetProperty("count").GetInt32()
                    });
                }
            }

            yield return pageResults;
            page++;
        }
    }

    public async IAsyncEnumerable<List<Series>> GetSeriesStreamAsync(string? rabbiId = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(rabbiId))
        {
            await foreach (var page in GetSeriesForRabbiStreamAsync(rabbiId, ct).WithCancellation(ct))
            {
                yield return page;
            }
            yield break;
        }

        // Stream all series (no rabbi filter)
        int pageNum = 1;
        int totalPages = 1;

        while (pageNum <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            var url = $"{BaseApiUrl}/shiurim-series?per_page=100&page={pageNum}&hide_empty=true&orderby=count&order=desc&_fields=id,name,count";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch series page {pageNum}: HTTP {(int)response.StatusCode}");
                break;
            }

            if (pageNum == 1 && response.Headers.TryGetValues("X-WP-TotalPages", out var totalPagesValues))
            {
                int.TryParse(totalPagesValues.FirstOrDefault(), out totalPages);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<JsonElement>(json);
            var pageResults = new List<Series>();

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    pageResults.Add(new Series
                    {
                        Id = item.GetProperty("id").GetInt32().ToString(),
                        Name = WebUtility.HtmlDecode(item.GetProperty("name").GetString() ?? string.Empty),
                        Count = item.GetProperty("count").GetInt32()
                    });
                }
            }

            yield return pageResults;
            pageNum++;
        }
    }

    /// <summary>
    /// Stream series for a specific rabbi. First collects all lesson pages to discover series IDs,
    /// then yields series details in chunks as they are fetched.
    /// </summary>
    private async IAsyncEnumerable<List<Series>> GetSeriesForRabbiStreamAsync(string rabbiId, [EnumeratorCancellation] CancellationToken ct)
    {
        var seriesLessonCount = new Dictionary<int, int>();

        // Step 1: Paginate through all lessons for this rabbi to collect series IDs and counts
        int page = 1;
        int totalPages = 1;

        while (page <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            var url = $"{BaseApiUrl}/shiurim?rabbis={rabbiId}&per_page=100&page={page}&_fields=shiurim-series";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch lessons for rabbi {rabbiId} page {page}: HTTP {(int)response.StatusCode}");
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
                    if (item.TryGetProperty("shiurim-series", out var seriesArray) && seriesArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var seriesId in seriesArray.EnumerateArray())
                        {
                            var id = seriesId.GetInt32();
                            if (seriesLessonCount.ContainsKey(id))
                                seriesLessonCount[id]++;
                            else
                                seriesLessonCount[id] = 1;
                        }
                    }
                }
            }

            page++;
        }

        if (seriesLessonCount.Count == 0)
            yield break;

        // Step 2: Fetch series details for the discovered IDs (in chunks of 100), yielding each chunk
        var seriesIds = seriesLessonCount.Keys.ToList();

        for (int i = 0; i < seriesIds.Count; i += 100)
        {
            ct.ThrowIfCancellationRequested();

            var chunk = seriesIds.Skip(i).Take(100);
            var includeParam = string.Join(",", chunk);
            var url = $"{BaseApiUrl}/shiurim-series?include={includeParam}&per_page=100&_fields=id,name,count";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to fetch series details: HTTP {(int)response.StatusCode}");
                continue;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<JsonElement>(json);
            var chunkResults = new List<Series>();

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();
                    var rabbiSpecificCount = seriesLessonCount.ContainsKey(id) ? seriesLessonCount[id] : 0;

                    if (rabbiSpecificCount > 0)
                    {
                        chunkResults.Add(new Series
                        {
                            Id = id.ToString(),
                            Name = WebUtility.HtmlDecode(item.GetProperty("name").GetString() ?? string.Empty),
                            Count = rabbiSpecificCount
                        });
                    }
                }
            }

            if (chunkResults.Count > 0)
                yield return chunkResults;
        }
    }

    public async Task<string> DownloadLessonAsync(Lesson lesson, string downloadPath, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        return await DownloadLessonInternalAsync(lesson, downloadPath, null, progress, ct);
    }

    public async Task<string> DownloadLessonAsync(Lesson lesson, string downloadPath, int index, IProgress<DownloadProgress> progress, CancellationToken ct = default)
    {
        return await DownloadLessonInternalAsync(lesson, downloadPath, index, progress, ct);
    }

    public async Task<string> ResolveAudioUrlAsync(string lessonLink, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(lessonLink))
                return string.Empty;

            Log($"Resolving audio URL from page: {lessonLink}");
            var response = await _httpClient.GetAsync(lessonLink, ct);
            if (!response.IsSuccessStatusCode)
            {
                Log($"Failed to fetch lesson page {lessonLink}: HTTP {(int)response.StatusCode}");
                return string.Empty;
            }

            var html = await response.Content.ReadAsStringAsync(ct);

            // Look for audio URL in the HTML - multiple patterns
            // Pattern 1: <audio ... src="https://mp3.meirtv.co.il/...mp3">
            var match = Regex.Match(html,
                @"https?://mp3\.meirtv\.co\.il[^""'\s<>]+\.mp3",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                Log($"Resolved audio URL (meirtv pattern): {match.Value}");
                return match.Value;
            }

            // Pattern 2: Any .mp3 URL on the page
            match = Regex.Match(html,
                @"https?://[^""'\s<>]+\.mp3",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                Log($"Resolved audio URL (generic mp3 pattern): {match.Value}");
                return match.Value;
            }

            Log($"No audio URL found on page {lessonLink}");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Log($"Error resolving audio URL for {lessonLink}: {ex.Message}");
            return string.Empty;
        }
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

            Log($"Downloading lesson '{lesson.Title}' from {lesson.AudioUrl} to {filePath}");

            using var response = await _httpClient.GetAsync(lesson.AudioUrl, HttpCompletionOption.ResponseHeadersRead, ct);

            if (!response.IsSuccessStatusCode)
            {
                Log($"Download failed for '{lesson.Title}': HTTP {(int)response.StatusCode}");
                throw new Exception($"Failed to download audio: HTTP {(int)response.StatusCode}");
            }

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

            Log($"Download complete for '{lesson.Title}': {filePath}");
            return filePath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Download error for '{lesson.Title}': {ex.Message}");
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
