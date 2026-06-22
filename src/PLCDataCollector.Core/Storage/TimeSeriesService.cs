using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Npgsql;
using PLCDataCollector.Core.Models;

namespace PLCDataCollector.Core.Storage;

public class TimeSeriesService : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private static readonly Regex SafeTableId = new(@"^\d+$", RegexOptions.Compiled);

    public TimeSeriesService(string connectionString)
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _dataSource = builder.Build();
    }

    public async Task EnsureTableAsync(int deviceId)
    {
        var deviceIdStr = deviceId.ToString();
        ValidateDeviceId(deviceIdStr);
        var tableName = SafeQuoteIdentifier($"t_data_{deviceIdStr}");

        await using var conn = await _dataSource.OpenConnectionAsync();
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                time TIMESTAMPTZ NOT NULL,
                point_id INTEGER NOT NULL,
                value DOUBLE PRECISION,
                quality SMALLINT DEFAULT 0,
                PRIMARY KEY (time, point_id)
            )";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();

        try
        {
            var hypertableSql = $"SELECT create_hypertable('{tableName}', 'time', if_not_exists => TRUE)";
            await using var hcmd = new NpgsqlCommand(hypertableSql, conn);
            await hcmd.ExecuteNonQueryAsync();
        }
        catch (Exception) { }
    }

    public async Task WritePoint(PointValue pv)
    {
        var deviceIdStr = pv.DeviceId.ToString();
        ValidateDeviceId(deviceIdStr);
        var tableName = SafeQuoteIdentifier($"t_data_{deviceIdStr}");

        await using var conn = await _dataSource.OpenConnectionAsync();
        var sql = $@"
            INSERT INTO {tableName} (time, point_id, value, quality)
            VALUES (@time, @pointId, @value, @quality)
        ";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("time", pv.Timestamp);
        cmd.Parameters.AddWithValue("pointId", pv.PointId);
        cmd.Parameters.AddWithValue("value", (object?)pv.Value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("quality", (short)pv.Quality);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<PointValue>> QueryHistoryAsync(
        int deviceId, int[] pointIds, DateTime from, DateTime to)
    {
        var deviceIdStr = deviceId.ToString();
        ValidateDeviceId(deviceIdStr);
        var tableName = SafeQuoteIdentifier($"t_data_{deviceIdStr}");

        var results = new List<PointValue>();
        await using var conn = await _dataSource.OpenConnectionAsync();

        var sql = $@"
            SELECT time, point_id, value, quality FROM {tableName}
            WHERE point_id = ANY(@pointIds) AND time BETWEEN @from AND @to
            ORDER BY time ASC
        ";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("pointIds", pointIds);
        cmd.Parameters.AddWithValue("from", from);
        cmd.Parameters.AddWithValue("to", to);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new PointValue
            {
                DeviceId = deviceId,
                PointId = reader.GetInt32(1),
                Value = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                Timestamp = reader.GetDateTime(0),
                Quality = (QualityStatus)reader.GetInt16(3)
            });
        }
        return results;
    }

    private static void ValidateDeviceId(string deviceId)
    {
        if (!SafeTableId.IsMatch(deviceId))
            throw new ArgumentException($"Invalid deviceId: {deviceId}", nameof(deviceId));
    }

    private static string SafeQuoteIdentifier(string tableName) =>
        NpgsqlCommandBuilder.QuoteIdentifier(tableName);

    public void Dispose()
    {
        _dataSource.Dispose();
    }
}
