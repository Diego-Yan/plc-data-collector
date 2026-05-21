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
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public Task ConnectAsync()
    {
        return Task.CompletedTask;
    }

    public Task SetPointValue(PointValue pv)
    {
        var key = $"point:{pv.DeviceId}:{pv.PointId}";
        _cache.Set(key, pv, DefaultExpiry);
        return Task.CompletedTask;
    }

    public Task<PointValue?> GetPointValue(string deviceId, string pointId)
    {
        var key = $"point:{deviceId}:{pointId}";
        var result = _cache.Get<PointValue?>(key);
        return Task.FromResult(result);
    }

    public Task SetDeviceStatus(string deviceId, bool online)
    {
        var key = $"device:{deviceId}:status";
        var status = new DeviceStatus
        {
            Online = online,
            LastSeen = DateTime.UtcNow.ToString("O")
        };
        _cache.Set(key, status, DefaultExpiry);
        return Task.CompletedTask;
    }

    public Task<DeviceStatus?> GetDeviceStatus(string deviceId)
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
