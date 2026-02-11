using System;
using System.Threading.Tasks;

namespace MeirDownloader.Core.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task ClearAsync();
}
