using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace MeirDownloader.Desktop.Services;

public class ImageCacheService
{
    private readonly HttpClient _httpClient;
    private readonly string _cacheDir;
    private readonly Dictionary<string, BitmapImage?> _memoryCache = new();

    public ImageCacheService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MeirDownloader/2.0");
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeirDownloader", "Cache", "Images");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<BitmapImage?> GetImageAsync(string imageUrl, string cacheKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(imageUrl)) return null;

        // Check memory cache
        if (_memoryCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Check disk cache
        var cachePath = Path.Combine(_cacheDir, $"{cacheKey}.jpg");
        if (File.Exists(cachePath))
        {
            var image = LoadImageFromFile(cachePath);
            _memoryCache[cacheKey] = image;
            return image;
        }

        // Download and cache
        try
        {
            var response = await _httpClient.GetAsync(imageUrl, ct);
            if (!response.IsSuccessStatusCode) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            await File.WriteAllBytesAsync(cachePath, bytes, ct);

            var downloadedImage = LoadImageFromBytes(bytes);
            _memoryCache[cacheKey] = downloadedImage;
            return downloadedImage;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error caching image {cacheKey}: {ex.Message}");
            return null;
        }
    }

    private static BitmapImage? LoadImageFromFile(string path)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.DecodePixelWidth = 80;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }

    private static BitmapImage? LoadImageFromBytes(byte[] bytes)
    {
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = new MemoryStream(bytes);
            image.DecodePixelWidth = 80;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch { return null; }
    }
}
