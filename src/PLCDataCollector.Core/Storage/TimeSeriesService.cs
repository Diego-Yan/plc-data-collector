using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using PLCDataCollector.Core.Models;

namespace PLCDataCollector.Core.Storage;

public class TimeSeriesService
{
    private readonly string _connectionString;

    public TimeSeriesService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task EnsureTableAsync(string deviceId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var tableName = $"t_data_{deviceId}";
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {tableName} (
                time TIMESTAMPTZ NOT NULL,
                point_id VARCHAR(64) NOT NULL,
                value DOUBLE PRECISION,
                quality SMALLINT DEFAULT 0,
                PRIMARY KEY (time, point_id)
            );
            SELECT create_hypertable('{tableName}', 'time', if_not_exists => TRUE);
        ";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task WritePoint(PointValue pv)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var tableName = $"t_data_{pv.DeviceId}";
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
        string deviceId, string[] pointIds, DateTime from, DateTime to)
    {
        var results = new List<PointValue>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var tableName = $"t_data_{deviceId}";
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
                PointId = reader.GetString(1),
                Value = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                Timestamp = reader.GetDateTime(0),
                Quality = (QualityStatus)reader.GetInt16(3)
            });
        }
        return results;
    }
}
