using MeirDownloader.Core.Models;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MeirDownloader.Core.Services;

public class MeirDownloaderService : IMeirDownloaderService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ICacheService _cacheService;
    private const string BaseApiUrl = "https://meirtv.com/wp-json/wp/v2";
    private const string AudioBaseUrl = "https://mp3.meirtv.co.il//wp2";
    private const int MaxConcurrency = 6;
    private readonly TimeSpan _defaultCacheExpiration = TimeSpan.FromHours(24);

    public MeirDownloaderService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MeirDownloader/2.0");
        _cacheService = new LiteDbCacheService();

        // Retry on HttpRequestException, 5xx errors, and 429 Too Many Requests
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && ((int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.TooManyRequests))
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                (outcome, timeSpan, retryCount, context) =>
                {
                    Log($"Request failed with {outcome.Result?.StatusCode}. Waiting {timeSpan} before retry {retryCount}.");
                });
    }

    private async Task<HttpResponseMessage> GetWithRetryAsync(string url, CancellationToken ct)
    {
        return await _retryPolicy.ExecuteAsync(async token =>
        {
            return await _httpClient.GetAsync(url, token);
        }, ct);
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
        var cacheKey = "rabbis_all";
        var cached = await _cacheService.GetAsync<List<Rabbi>>(cacheKey);
        if (cached != null) return cached;

        var allRabbis = new ConcurrentBag<Rabbi>();
        int totalPages = 1;

        try
        {
            // Fetch page 1 to get total pages
            var url1 = $"{BaseApiUrl}/rabbis?per_page=100&page=1&orderby=count&order=desc&_fields=id,name,slug,count,link";
            var response1 = await GetWithRetryAsync(url1, ct);

            if (!response1.IsSuccessStatusCode)
            {
                Log($"Failed to fetch rabbis page 1: HTTP {(int)response1.StatusCode}");
                return new List<Rabbi>();
            }

            if (response1.Headers.TryGetValues("X-WP-TotalPages", out var totalPagesValues))
            {
                int.TryParse(totalPagesValues.FirstOrDefault(), out totalPages);
            }

            var json1 = await response1.Content.ReadAsStringAsync(ct);
            ProcessRabbisJson(json1, allRabbis);

            if (totalPages > 1)
            {
                var semaphore = new SemaphoreSlim(MaxConcurrency);
                var tasks = Enumerable.Range(2, totalPages - 1).Select(async page =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var url = $"{BaseApiUrl}/rabbis?per_page=100&page={page}&orderby=count&order=desc&_fields=id,name,slug,count,link";
                        var response = await GetWithRetryAsync(url, ct);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(ct);
                            ProcessRabbisJson(json, allRabbis);
                        }
                        else
                        {
                            Log($"Failed to fetch rabbis page {page}: HTTP {(int)response.StatusCode}");
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            Log($"Error fetching rabbis: {ex.Message}");
        }

        var result = allRabbis.ToList();
        if (result.Any())
        {
            await _cacheService.SetAsync(cacheKey, result, _defaultCacheExpiration);
        }
        return result;
    }

    private void ProcessRabbisJson(string json, ConcurrentBag<Rabbi> collection)
    {
        var items = JsonSerializer.Deserialize<JsonElement>(json);
        if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                collection.Add(new Rabbi
                {
                    Id = item.GetProperty("id").GetInt32().ToString(),
                    Name = item.GetProperty("name").GetString() ?? string.Empty,
                    Count = item.GetProperty("count").GetInt32(),
                    Link = item.TryGetProperty("link", out var linkProp) ? linkProp.GetString() ?? string.Empty : string.Empty
                });
            }
        }
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
        var seriesLessonCount = new ConcurrentDictionary<int, int>();

        try
        {
            // Step 1: Paginate through all lessons for this rabbi to collect series IDs and counts
            int totalPages = 1;
            
            // Fetch page 1
            var url1 = $"{BaseApiUrl}/shiurim?rabbis={rabbiId}&per_page=100&page=1&_fields=shiurim-series";
            var response1 = await GetWithRetryAsync(url1, ct);

            if (!response1.IsSuccessStatusCode)
            {
                Log($"Failed to fetch lessons for rabbi {rabbiId} page 1: HTTP {(int)response1.StatusCode}");
                return new List<Series>();
            }

            if (response1.Headers.TryGetValues("X-WP-TotalPages", out var totalPagesValues))
            {
                int.TryParse(totalPagesValues.FirstOrDefault(), out totalPages);
            }

            var json1 = await response1.Content.ReadAsStringAsync(ct);
            ProcessSeriesIdsFromJson(json1, seriesLessonCount);

            if (totalPages > 1)
            {
                var semaphore = new SemaphoreSlim(MaxConcurrency);
                var tasks = Enumerable.Range(2, totalPages - 1).Select(async page =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var url = $"{BaseApiUrl}/shiurim?rabbis={rabbiId}&per_page=100&page={page}&_fields=shiurim-series";
                        var response = await GetWithRetryAsync(url, ct);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(ct);
                            ProcessSeriesIdsFromJson(json, seriesLessonCount);
                        }
                        else
                        {
                            Log($"Failed to fetch lessons for rabbi {rabbiId} page {page}: HTTP {(int)response.StatusCode}");
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Error fetching lessons for rabbi {rabbiId}: {ex.Message}");
        }

        if (seriesLessonCount.IsEmpty)
            return new List<Series>();

        // Step 2: Fetch series details for the discovered IDs (in chunks of 100)
        var allSeries = new ConcurrentBag<Series>();
        var seriesIds = seriesLessonCount.Keys.ToList();

        try
        {
            var chunks = seriesIds.Chunk(100).ToList();
            var semaphore = new SemaphoreSlim(MaxConcurrency);
            
            var tasks = chunks.Select(async chunk =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    var includeParam = string.Join(",", chunk);
                    var url = $"{BaseApiUrl}/shiurim-series?include={includeParam}&per_page=100&_fields=id,name,count";
                    var response = await GetWithRetryAsync(url, ct);

                    if (response.IsSuccessStatusCode)
                    {
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
                    else
                    {
                        Log($"Failed to fetch series details: HTTP {(int)response.StatusCode}");
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Error fetching series details: {ex.Message}");
        }

        // Sort by rabbi-specific lesson count descending
        return allSeries.OrderByDescending(s => s.Count).ToList();
    }

    private void ProcessSeriesIdsFromJson(string json, ConcurrentDictionary<int, int> seriesLessonCount)
    {
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
                        seriesLessonCount.AddOrUpdate(id, 1, (key, oldValue) => oldValue + 1);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fetch all series with hide_empty=true to exclude 0-count series.
    /// </summary>
    private async Task<List<Series>> GetAllSeriesAsync(CancellationToken ct)
    {
        var allSeries = new ConcurrentBag<Series>();

        try
        {
            int totalPages = 1;
            
            // Fetch page 1
            var url1 = $"{BaseApiUrl}/shiurim-series?per_page=100&page=1&hide_empty=true&orderby=count&order=desc&_fields=id,name,count";
            var response1 = await GetWithRetryAsync(url1, ct);

            if (!response1.IsSuccessStatusCode)
            {
                Log($"Failed to fetch series page 1: HTTP {(int)response1.StatusCode}");
                return new List<Series>();
            }

            if (response1.Headers.TryGetValues("X-WP-TotalPages", out var totalPagesValues))
            {
                int.TryParse(totalPagesValues.FirstOrDefault(), out totalPages);
            }

            var json1 = await response1.Content.ReadAsStringAsync(ct);
            ProcessSeriesJson(json1, allSeries);

            if (totalPages > 1)
            {
                var semaphore = new SemaphoreSlim(MaxConcurrency);
                var tasks = Enumerable.Range(2, totalPages - 1).Select(async page =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var url = $"{BaseApiUrl}/shiurim-series?per_page=100&page={page}&hide_empty=true&orderby=count&order=desc&_fields=id,name,count";
                        var response = await GetWithRetryAsync(url, ct);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(ct);
                            ProcessSeriesJson(json, allSeries);
                        }
                        else
                        {
                            Log($"Failed to fetch series page {page}: HTTP {(int)response.StatusCode}");
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            Log($"Error fetching series: {ex.Message}");
        }

        return allSeries.ToList();
    }

    private void ProcessSeriesJson(string json, ConcurrentBag<Series> collection)
    {
        var items = JsonSerializer.Deserialize<JsonElement>(json);
        if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                collection.Add(new Series
                {
                    Id = item.GetProperty("id").GetInt32().ToString(),
                    Name = WebUtility.HtmlDecode(item.GetProperty("name").GetString() ?? string.Empty),
                    Count = item.GetProperty("count").GetInt32()
                });
            }
        }
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

            var response = await GetWithRetryAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                Log($"Failed to fetch lessons page {page}: HTTP {(int)response.StatusCode}");
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
            Log($"Error fetching lessons: {ex.Message}");
        }

        return lessons;
    }

    public async Task<List<Lesson>> GetAllLessonsAsync(string? rabbiId = null, string? seriesId = null, CancellationToken ct = default)
    {
        var cacheKey = $"lessons_r{rabbiId}_s{seriesId}";
        var cached = await _cacheService.GetAsync<List<Lesson>>(cacheKey);
        if (cached != null) return cached;

        var allLessons = new ConcurrentBag<Lesson>();

        try
        {
            int totalPages = 1;
            
            // Build base URL
            var baseUrl = $"{BaseApiUrl}/shiurim?per_page=100&_fields=id,title,date,rabbis,shiurim-series,link";
            if (!string.IsNullOrEmpty(rabbiId))
                baseUrl += $"&rabbis={rabbiId}";
            if (!string.IsNullOrEmpty(seriesId))
                baseUrl += $"&shiurim-series={seriesId}";

            // Fetch page 1
            var url1 = $"{baseUrl}&page=1";
            var response1 = await GetWithRetryAsync(url1, ct);

            if (!response1.IsSuccessStatusCode)
            {
                Log($"Failed to fetch lessons page 1: HTTP {(int)response1.StatusCode}");
                return new List<Lesson>();
            }

            if (response1.Headers.TryGetValues("X-WP-TotalPages", out var totalPagesValues))
            {
                int.TryParse(totalPagesValues.FirstOrDefault(), out totalPages);
            }

            var json1 = await response1.Content.ReadAsStringAsync(ct);
            ProcessLessonsJson(json1, allLessons);

            if (totalPages > 1)
            {
                var semaphore = new SemaphoreSlim(MaxConcurrency);
                var tasks = Enumerable.Range(2, totalPages - 1).Select(async page =>
                {
                    await semaphore.WaitAsync(ct);
                    try
                    {
                        var url = $"{baseUrl}&page={page}";
                        var response = await GetWithRetryAsync(url, ct);

                        if (response.IsSuccessStatusCode)
                        {
                            var json = await response.Content.ReadAsStringAsync(ct);
                            ProcessLessonsJson(json, allLessons);
                        }
                        else
                        {
                            Log($"Failed to fetch lessons page {page}: HTTP {(int)response.StatusCode}");
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                await Task.WhenAll(tasks);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log($"Error fetching all lessons: {ex.Message}");
        }

        // Sort by date ascending (oldest first) so numbering makes chronological sense
        var result = allLessons.OrderBy(l => l.Date).ToList();
        
        if (result.Any())
        {
            await _cacheService.SetAsync(cacheKey, result, _defaultCacheExpiration);
        }

        return result;
    }

    private void ProcessLessonsJson(string json, ConcurrentBag<Lesson> collection)
    {
        var items = JsonSerializer.Deserialize<JsonElement>(json);
        if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var id = item.GetProperty("id").GetInt32();
                var titleRendered = item.GetProperty("title").GetProperty("rendered").GetString() ?? string.Empty;
                var link = item.TryGetProperty("link", out var linkProp) ? linkProp.GetString() ?? string.Empty : string.Empty;

                collection.Add(new Lesson
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

    public async IAsyncEnumerable<List<Rabbi>> GetRabbisStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        // Check cache first
        var cacheKey = "rabbis_all";
        var cached = await _cacheService.GetAsync<List<Rabbi>>(cacheKey);
        if (cached != null)
        {
            yield return cached;
            yield break;
        }

        var allRabbis = new List<Rabbi>();
        int page = 1;
        int totalPages = 1;

        while (page <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            var url = $"{BaseApiUrl}/rabbis?per_page=100&page={page}&orderby=count&order=desc&_fields=id,name,slug,count,link";
            var response = await GetWithRetryAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                Log($"Failed to fetch rabbis page {page}: HTTP {(int)response.StatusCode}");
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
                        Count = item.GetProperty("count").GetInt32(),
                        Link = item.TryGetProperty("link", out var linkProp) ? linkProp.GetString() ?? string.Empty : string.Empty
                    });
                }
            }

            allRabbis.AddRange(pageResults);
            yield return pageResults;
            page++;
        }

        if (allRabbis.Any())
        {
            await _cacheService.SetAsync(cacheKey, allRabbis, _defaultCacheExpiration);
        }
    }

    public async IAsyncEnumerable<List<Series>> GetSeriesStreamAsync(string? rabbiId = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(rabbiId))
        {
            // Check cache first — fast path
            var cacheKey = $"series_r{rabbiId}";
            var cached = await _cacheService.GetAsync<List<Series>>(cacheKey);
            if (cached != null)
            {
                yield return cached;
                yield break;
            }

            // Cache miss — stream pages as they arrive for UI responsiveness,
            // accumulate results, and fire-and-forget cache write after completion.
            var allSeries = new List<Series>();
            await foreach (var page in GetSeriesForRabbiStreamAsync(rabbiId, ct).WithCancellation(ct))
            {
                allSeries.AddRange(page);
                yield return page;  // Yield each page immediately for responsive UI
            }

            // Fire-and-forget cache write — not awaited so it completes independently.
            // If the caller cancelled mid-stream, we won't reach here, which is fine:
            // next time the user selects this rabbi it will be a cache miss again
            // and will eventually complete a full load to populate the cache.
            if (allSeries.Count > 0)
            {
                _ = _cacheService.SetAsync(cacheKey, allSeries, _defaultCacheExpiration);
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
            var response = await GetWithRetryAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                Log($"Failed to fetch series page {pageNum}: HTTP {(int)response.StatusCode}");
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
    /// Stream series for a specific rabbi. Scans lesson pages one at a time and yields
    /// newly discovered series immediately, so the UI populates incrementally.
    /// </summary>
    private async IAsyncEnumerable<List<Series>> GetSeriesForRabbiStreamAsync(string rabbiId, [EnumeratorCancellation] CancellationToken ct)
    {
        var seenSeriesIds = new HashSet<int>();
        var seriesCountMap = new Dictionary<int, int>(); // seriesId -> lesson count
        int page = 1;
        int totalPages = 1;

        while (page <= totalPages)
        {
            ct.ThrowIfCancellationRequested();

            // Fetch one page of lessons
            var url = $"{BaseApiUrl}/shiurim?rabbis={rabbiId}&per_page=100&page={page}&_fields=shiurim-series";
            var response = await GetWithRetryAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                Log($"Failed to fetch lessons for rabbi {rabbiId} page {page}: HTTP {(int)response.StatusCode}");
                break;
            }

            if (page == 1 && response.Headers.TryGetValues("X-WP-TotalPages", out var tp))
            {
                int.TryParse(tp.FirstOrDefault(), out totalPages);
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<JsonElement>(json);

            var newSeriesIds = new List<int>();

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("shiurim-series", out var seriesArray) && seriesArray.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var sid in seriesArray.EnumerateArray())
                        {
                            var seriesId = sid.GetInt32();
                            seriesCountMap[seriesId] = seriesCountMap.GetValueOrDefault(seriesId) + 1;

                            if (seenSeriesIds.Add(seriesId))
                            {
                                newSeriesIds.Add(seriesId);
                            }
                        }
                    }
                }
            }

            // If we found new series IDs on this page, fetch their details and yield immediately
            if (newSeriesIds.Count > 0)
            {
                var seriesDetails = await FetchSeriesDetailsByIds(newSeriesIds, seriesCountMap, ct);
                if (seriesDetails.Count > 0)
                {
                    yield return seriesDetails;
                }
            }

            page++;
        }
    }

    /// <summary>
    /// Fetch series details (name, count) for a list of series IDs, using the rabbi-specific
    /// lesson counts from the countMap.
    /// </summary>
    private async Task<List<Series>> FetchSeriesDetailsByIds(List<int> seriesIds, Dictionary<int, int> countMap, CancellationToken ct)
    {
        var result = new List<Series>();

        // Chunk into batches of 100 (WordPress API limit)
        foreach (var chunk in seriesIds.Chunk(100))
        {
            ct.ThrowIfCancellationRequested();

            var ids = string.Join(",", chunk);
            var url = $"{BaseApiUrl}/shiurim-series?include={ids}&per_page=100&_fields=id,name,count";
            var response = await GetWithRetryAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                Log($"Failed to fetch series details: HTTP {(int)response.StatusCode}");
                continue;
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var items = JsonSerializer.Deserialize<JsonElement>(json);

            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetInt32();
                    var rabbiSpecificCount = countMap.GetValueOrDefault(id, 0);

                    if (rabbiSpecificCount > 0)
                    {
                        result.Add(new Series
                        {
                            Id = id.ToString(),
                            Name = WebUtility.HtmlDecode(item.GetProperty("name").GetString() ?? string.Empty),
                            Count = rabbiSpecificCount
                        });
                    }
                }
            }
        }

        return result;
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
            var response = await GetWithRetryAsync(lessonLink, ct);
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

            // Use retry policy for the initial connection
            using var response = await _retryPolicy.ExecuteAsync(async token =>
            {
                return await _httpClient.GetAsync(lesson.AudioUrl, HttpCompletionOption.ResponseHeadersRead, token);
            }, ct);

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

    public async Task<string> GetRabbiImageUrlAsync(string rabbiLink, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrEmpty(rabbiLink)) return string.Empty;

            var response = await GetWithRetryAsync(rabbiLink, ct);
            if (!response.IsSuccessStatusCode) return string.Empty;

            var html = await response.Content.ReadAsStringAsync(ct);

            // Strategy 1: Look for jet-listing-dynamic-image
            var match = Regex.Match(html,
                @"jet-listing-dynamic-image[^>]*>\s*<img[^>]+src=""([^""]+)""",
                RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            // Strategy 2: Look for thumbnailUrl in Yoast JSON-LD
            match = Regex.Match(html,
                @"""thumbnailUrl""\s*:\s*""([^""]+)""",
                RegexOptions.IgnoreCase);
            if (match.Success) return match.Groups[1].Value;

            return string.Empty;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error getting rabbi image: {ex.Message}");
            return string.Empty;
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

    public void Dispose()
    {
        if (_cacheService is IDisposable disposableCache)
        {
            disposableCache.Dispose();
        }
        _httpClient.Dispose();
    }
}
