// ============================================================================
// TAG: merged-from-service — 2026-05-20
// Moved from PLCDataCollector.Service to PLCDataCollector.Web (single-process merge).
// Changed: injects WebSocketHandler directly (no more stub WebSocketBroadcaster).
// Changed: "Redis连接失败" log → "缓存服务初始化".
// ============================================================================

// TAG: merged-from-service — 2026-05-20
// TAG: config-wired — reads CollectIntervalMs from IConfiguration instead of hardcoded 1000ms
using System;
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
    private readonly Dictionary<string, PlcConnection> _connections = new();

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

        try { await _cacheService.ConnectAsync(); }
        catch (Exception ex) { _logger.LogWarning(ex, "缓存服务初始化异常"); }

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

        foreach (var device in devices)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var conn = GetOrCreateConnection(device);
                if (conn == null) continue;

                if (!conn.IsConnected)
                {
                    var ok = await conn.ConnectAsync();
                    if (!ok)
                    {
                        device.IsOnline = false;
                        await _cacheService.SetDeviceStatus(device.Id.ToString(), false);
                        await _wsHandler.BroadcastStatus(device.Id.ToString(), false);
                        continue;
                    }
                    device.IsOnline = true;
                }

                var points = _deviceManager.GetDevicePoints(device.Id.ToString());
                foreach (var point in points)
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
                        DeviceId = device.Id.ToString(),
                        PointId = point.Id.ToString(),
                        Value = value,
                        Timestamp = DateTime.UtcNow,
                        Quality = value.HasValue ? QualityStatus.Good : QualityStatus.BadValue
                    };

                    await _cacheService.SetPointValue(pv);
                    await _tsService.WritePoint(pv);
                    await _wsHandler.BroadcastPoint(pv);
                }

                device.LastCollectedAt = DateTime.UtcNow;
                device.IsOnline = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "采集设备 {Name}({Ip}) 异常", device.Name, device.IpAddress);
                device.IsOnline = false;
            }
        }

        // TAG: fixed — purge connections for deleted devices
        PruneConnections(devices);
    }

    /// <summary>
    /// Remove connections for devices that no longer exist (deleted via API).
    /// </summary>
    private void PruneConnections(List<Device> activeDevices)
    {
        var activeIds = new HashSet<string>(activeDevices.Select(d => d.Id.ToString()));
        var staleIds = _connections.Keys.Where(k => !activeIds.Contains(k)).ToList();
        foreach (var id in staleIds)
        {
            if (_connections.Remove(id, out var conn))
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
            try { conn.Dispose(); } catch { }
        }
        _connections.Clear();
        await base.StopAsync(cancellationToken);
    }

    private PlcConnection? GetOrCreateConnection(Device device)
    {
        var id = device.Id.ToString();
        if (_connections.TryGetValue(id, out var conn))
        {
            // TAG: fixed — if device IP/port changed, replace stale connection
            if (conn.Ip != device.IpAddress || conn.Port != device.Port)
            {
                try { conn.Dispose(); } catch { }
                _connections.Remove(id);
            }
            else
            {
                return conn;
            }
        }

        conn = new PlcConnection(id, device.IpAddress, device.Port, device.Rack, device.Slot);
        _connections[id] = conn;
        return conn;
    }
}
