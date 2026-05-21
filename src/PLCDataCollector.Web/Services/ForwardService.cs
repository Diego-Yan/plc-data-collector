// ============================================================================
// TAG: rewritten — 2026-05-20
// ForwardService was a stub (only log + Task.CompletedTask). Now it actually
// reads cached point values and inserts snapshots into the relational DB wide
// table r_data_{deviceId} (JSON column for dynamic point schema).
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using PLCDataCollector.Core.Cache;
using PLCDataCollector.Core.Models;
using PLCDataCollector.Web.Services;

namespace PLCDataCollector.Web.Services;

public class ForwardService : BackgroundService
{
    private readonly ILogger<ForwardService> _logger;
    private readonly MemoryCacheService _cache;
    private readonly DeviceManager _deviceManager;
    private readonly string _connString;
    private readonly int _intervalSec;

    public ForwardService(
        ILogger<ForwardService> logger,
        MemoryCacheService cache,
        DeviceManager deviceManager,
        IConfiguration config)
    {
        _logger = logger;
        _cache = cache;
        _deviceManager = deviceManager;
        _connString = config.GetConnectionString("RelationalDb")
            ?? "Host=127.0.0.1;Database=plc_data_forward;Username=postgres";
        _intervalSec = config.GetValue<int>("Forward:IntervalSec", 10);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("转发服务启动，间隔 {Interval}s", _intervalSec);
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ForwardCycleAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(_intervalSec), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转发周期异常");
                await Task.Delay(10000, stoppingToken);
            }
        }
    }

    private async Task ForwardCycleAsync(CancellationToken ct)
    {
        var devices = _deviceManager.GetActiveDevices();
        if (devices.Count == 0) return;

        try
        {
            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync(ct);

            foreach (var device in devices)
            {
                if (ct.IsCancellationRequested) break;

                var points = _deviceManager.GetDevicePoints(device.Id.ToString());
                if (points.Count == 0) continue;

                var snapshot = new Dictionary<string, object?>();
                foreach (var point in points)
                {
                    var pv = await _cache.GetPointValue(device.Id.ToString(), point.Id.ToString());
                    snapshot[point.Code] = pv?.Value;
                }

                var tableName = $"r_data_{device.Id}";
                await EnsureWideTable(conn, tableName, ct);

                var sql = $@"
                    INSERT INTO {tableName} (time, data)
                    VALUES (@time, @data::jsonb)
                ";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("time", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(snapshot));
                await cmd.ExecuteNonQueryAsync(ct);
            }

            _logger.LogDebug("转发周期完成，处理 {Count} 个设备", devices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转发写入失败");
        }
    }

    private static async Task EnsureWideTable(NpgsqlConnection conn, string tableName, CancellationToken ct)
    {
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                time TIMESTAMPTZ NOT NULL PRIMARY KEY,
                data JSONB NOT NULL
            )
        ";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
