// ============================================================================
// TAG: rewritten — 2026-05-20
// TAG: review-fix — 2026-06-22 — SQL injection protection, connection pooling,
//      per-device error isolation, safe table name quoting.
// TAG: review-fix-2 — 2026-06-22 — IAsyncDisposable to release NpgsqlDataSource,
//      cache EnsureWideTable with static ConcurrentDictionary to avoid
//      redundant CREATE TABLE IF NOT EXISTS every cycle.
// ============================================================================

using System;
using System.Collections.Concurrent;
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

namespace PLCDataCollector.Web.Services;

public class ForwardService : BackgroundService, IAsyncDisposable
{
    private static readonly ConcurrentDictionary<string, bool> _tablesEnsured = new();

    private readonly ILogger<ForwardService> _logger;
    private readonly MemoryCacheService _cache;
    private readonly DeviceManager _deviceManager;
    private readonly NpgsqlDataSource _dataSource;
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
        var connStr = config.GetConnectionString("RelationalDb")
            ?? "Host=127.0.0.1;Database=plc_data_forward;Username=postgres";
        _dataSource = new NpgsqlDataSourceBuilder(connStr).Build();
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

        await using var conn = await _dataSource.OpenConnectionAsync(ct);

        foreach (var device in devices)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var points = _deviceManager.GetDevicePoints(device.Id.ToString());
                if (points.Count == 0) continue;

                var snapshot = new Dictionary<string, object?>();
                foreach (var point in points)
                {
                    var pv = await _cache.GetPointValue(device.Id, point.Id);
                    snapshot[point.Code] = pv?.Value;
                }

                var tableName = SafeQuoteIdentifier($"r_data_{device.Id}");
                await EnsureWideTableOnce(conn, tableName, ct);

                var sql = $@"
                    INSERT INTO {tableName} (time, data)
                    VALUES (@time, @data::jsonb)
                ";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("time", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("data", JsonSerializer.Serialize(snapshot));
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "转发设备 {Id} 失败", device.Id);
            }
        }

        _logger.LogDebug("转发周期完成，处理 {Count} 个设备", devices.Count);
    }

    private static async Task EnsureWideTableOnce(NpgsqlConnection conn, string tableName, CancellationToken ct)
    {
        if (!_tablesEnsured.TryAdd(tableName, true)) return;

        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                time TIMESTAMPTZ NOT NULL PRIMARY KEY,
                data JSONB NOT NULL
            )
        ";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string SafeQuoteIdentifier(string tableName) =>
        NpgsqlCommandBuilder.QuoteIdentifier(tableName);

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }
}
