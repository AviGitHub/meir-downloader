using System;
using System.IO;
using System.Threading.Tasks;
using LiteDB;

namespace MeirDownloader.Core.Services;

public class LiteDbCacheService : ICacheService, IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<CacheItem> _collection;

    public LiteDbCacheService()
    {
        var cachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MeirDownloader", "Cache", "data.db");
        
        Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
        
        _db = new LiteDatabase(cachePath);
        _collection = _db.GetCollection<CacheItem>("cache");
        _collection.EnsureIndex(x => x.Key);
    }

    public Task<T?> GetAsync<T>(string key)
    {
        return Task.Run(() =>
        {
            var item = _collection.FindOne(x => x.Key == key);
            
            if (item == null)
                return default;

            if (item.Expiration.HasValue && item.Expiration.Value < DateTime.UtcNow)
            {
                _collection.Delete(item.Id);
                return default;
            }

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<T>(item.Value);
            }
            catch
            {
                return default;
            }
        });
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        return Task.Run(() =>
        {
            var json = System.Text.Json.JsonSerializer.Serialize(value);
            var item = new CacheItem
            {
                Key = key,
                Value = json,
                Expiration = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : null
            };

            _collection.Upsert(item);
        });
    }

    public Task ClearAsync()
    {
        return Task.Run(() =>
        {
            _collection.DeleteAll();
        });
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private class CacheItem
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime? Expiration { get; set; }
    }
}
