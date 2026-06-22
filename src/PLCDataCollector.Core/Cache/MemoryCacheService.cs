using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using PLCDataCollector.Core.Models;

namespace PLCDataCollector.Core.Cache;

public class MemoryCacheService : IDisposable
{
    private readonly MemoryCache _cache;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    public MemoryCacheService()
    {
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 50000
        });
    }

    public Task SetPointValue(PointValue pv)
    {
        var key = $"point:{pv.DeviceId}:{pv.PointId}";
        _cache.Set(key, pv, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultExpiry,
            Size = 1
        });
        return Task.CompletedTask;
    }

    public Task<PointValue?> GetPointValue(int deviceId, int pointId)
    {
        var key = $"point:{deviceId}:{pointId}";
        var result = _cache.Get<PointValue?>(key);
        return Task.FromResult(result);
    }

    public Task SetDeviceStatus(int deviceId, bool online)
    {
        var key = $"device:{deviceId}:status";
        var status = new DeviceStatus
        {
            Online = online,
            LastSeen = DateTime.UtcNow.ToString("O")
        };
        _cache.Set(key, status, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = DefaultExpiry,
            Size = 1
        });
        return Task.CompletedTask;
    }

    public Task<DeviceStatus?> GetDeviceStatus(int deviceId)
    {
        var key = $"device:{deviceId}:status";
        var result = _cache.Get<DeviceStatus?>(key);
        return Task.FromResult(result);
    }

    public void Dispose()
    {
        _cache.Dispose();
    }
}

public class DeviceStatus
{
    public bool Online { get; set; }
    public string LastSeen { get; set; } = "";
}
