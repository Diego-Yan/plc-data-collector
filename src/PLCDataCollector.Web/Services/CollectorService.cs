// ============================================================================
// TAG: merged-from-service — 2026-05-20
// TAG: review-fix — 2026-06-22 — thread-safe ConcurrentDictionary, concurrent device
//      collection, per-point exception isolation, online status write-back.
// TAG: review-fix-2 — 2026-06-22 — GetOrCreateConnection TryAdd to prevent leak.
//      PointValue.DeviceId/PointId now int. Removed .ToString() conversions.
// ============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PLCDataCollector.Core.Cache;
using PLCDataCollector.Core.Models;
using PLCDataCollector.Core.Plc;
using PLCDataCollector.Core.Storage;
using PLCDataCollector.Web.WebSocket;

namespace PLCDataCollector.Web.Services;

public class CollectorService : BackgroundService
{
    private readonly ILogger<CollectorService> _logger;
    private readonly DeviceManager _deviceManager;
    private readonly MemoryCacheService _cacheService;
    private readonly TimeSeriesService _tsService;
    private readonly WebSocketHandler _wsHandler;
    private readonly int _collectIntervalMs;
    private readonly ConcurrentDictionary<string, PlcConnection> _connections = new();

    public CollectorService(
        ILogger<CollectorService> logger,
        DeviceManager deviceManager,
        MemoryCacheService cacheService,
        TimeSeriesService tsService,
        WebSocketHandler wsHandler,
        IConfiguration config)
    {
        _logger = logger;
        _deviceManager = deviceManager;
        _cacheService = cacheService;
        _tsService = tsService;
        _wsHandler = wsHandler;
        _collectIntervalMs = config.GetValue<int>("Collector:CollectIntervalMs", 1000);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PLC采集服务启动，延迟5秒后开始采集");
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectCycleAsync(stoppingToken);
                await Task.Delay(_collectIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "采集周期异常");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task CollectCycleAsync(CancellationToken ct)
    {
        var devices = _deviceManager.GetActiveDevices();
        _logger.LogDebug("采集周期开始，共 {Count} 个活跃设备", devices.Count);

        var tasks = devices.Select(device => CollectDeviceAsync(device, ct));
        await Task.WhenAll(tasks);

        PruneConnections(devices);
    }

    private async Task CollectDeviceAsync(Device device, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;

        try
        {
            var conn = GetOrCreateConnection(device);
            if (conn == null) return;

            if (!conn.IsConnected)
            {
                var ok = await conn.ConnectAsync();
                if (!ok)
                {
                    _deviceManager.UpdateOnlineStatus(device.Id, false);
                    await _cacheService.SetDeviceStatus(device.Id, false);
                    await _wsHandler.BroadcastStatus(device.Id, false);
                    return;
                }
            }

            var points = _deviceManager.GetDevicePoints(device.Id);
            foreach (var point in points)
            {
                if (ct.IsCancellationRequested) break;

                try
                {
                    var rawValue = await conn.ReadAsync(point.Address);
                    double? value = null;

                    if (rawValue != null)
                    {
                        try { value = Convert.ToDouble(rawValue); }
                        catch { value = null; }
                    }

                    var pv = new PointValue
                    {
                        DeviceId = device.Id,
                        PointId = point.Id,
                        Value = value,
                        Timestamp = DateTime.UtcNow,
                        Quality = value.HasValue ? QualityStatus.Good : QualityStatus.BadValue
                    };

                    try { await _cacheService.SetPointValue(pv); }
                    catch (Exception ex) { _logger.LogWarning(ex, "缓存写入失败 {Device}/{Point}", device.Id, point.Id); }

                    try { await _tsService.WritePoint(pv); }
                    catch (Exception ex) { _logger.LogWarning(ex, "时序写入失败 {Device}/{Point}", device.Id, point.Id); }

                    try { await _wsHandler.BroadcastPoint(pv); }
                    catch (Exception ex) { _logger.LogWarning(ex, "广播失败 {Device}/{Point}", device.Id, point.Id); }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "采集点位 {Address} 失败", point.Address);
                }
            }

            _deviceManager.UpdateOnlineStatus(device.Id, true);
            await _cacheService.SetDeviceStatus(device.Id, true);
            await _wsHandler.BroadcastStatus(device.Id, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "采集设备 {Name}({Ip}) 异常", device.Name, device.IpAddress);
            _deviceManager.UpdateOnlineStatus(device.Id, false);
            await _cacheService.SetDeviceStatus(device.Id, false);
            await _wsHandler.BroadcastStatus(device.Id, false);
        }
    }

    private void PruneConnections(List<Device> activeDevices)
    {
        var activeIds = new HashSet<string>(activeDevices.Select(d => d.Id.ToString()));
        var staleIds = _connections.Keys.Where(k => !activeIds.Contains(k)).ToList();
        foreach (var id in staleIds)
        {
            if (_connections.TryRemove(id, out var conn))
            {
                try { conn.Dispose(); } catch { }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PLC采集服务停止，清理 {Count} 个连接", _connections.Count);
        foreach (var conn in _connections.Values)
        {
            try
            {
                if (conn is IAsyncDisposable ad)
                    await ad.DisposeAsync();
                else
                    conn.Dispose();
            }
            catch { }
        }
        _connections.Clear();
        await base.StopAsync(cancellationToken);
    }

    private PlcConnection? GetOrCreateConnection(Device device)
    {
        var id = device.Id.ToString();

        if (_connections.TryGetValue(id, out var existing))
        {
            if (existing.Ip == device.IpAddress && existing.Port == device.Port)
                return existing;

            if (_connections.TryRemove(id, out var old))
            {
                try { old.Dispose(); } catch { }
            }
        }

        var newConn = new PlcConnection(id, device.IpAddress, device.Port, device.Rack, device.Slot);
        return _connections.TryAdd(id, newConn) ? newConn : _connections.GetValueOrDefault(id);
    }
}
