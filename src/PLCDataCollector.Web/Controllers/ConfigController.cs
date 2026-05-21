// ============================================================================
// TAG: rewritten — 2026-05-20 — JSON file persistence, thread-safe, typed system config
// TAG: thread-safe — added lock around file Load/Save
// TAG: type-fix — system config GET now returns ints (not strings) for numeric fields
// ============================================================================

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace PLCDataCollector.Web.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private static readonly string ConfigDir = Path.Combine(AppContext.BaseDirectory, "config");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "system_config.json");
    private static readonly object _fileLock = new();

    // ---- internal storage ----

    private static Dictionary<string, string> Load()
    {
        lock (_fileLock)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                if (System.IO.File.Exists(ConfigPath))
                {
                    var json = System.IO.File.ReadAllText(ConfigPath);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? Defaults();
                }
            }
            catch { }
            return Defaults();
        }
    }

    private static void Save(Dictionary<string, string> cfg)
    {
        lock (_fileLock)
        {
            try
            {
                Directory.CreateDirectory(ConfigDir);
                System.IO.File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg));
            }
            catch { }
        }
    }

    private static Dictionary<string, string> Defaults() => new()
    {
        ["timescaledb_host"] = "127.0.0.1",
        ["timescaledb_port"] = "5432",
        ["timescaledb_database"] = "plc_data",
        ["timescaledb_username"] = "postgres",
        ["relational_host"] = "127.0.0.1",
        ["relational_port"] = "5432",
        ["relational_database"] = "plc_data_forward",
        ["relational_username"] = "postgres",
        ["collect_interval"] = "1000",
        ["timeout"] = "3000",
        ["reconnect_interval"] = "10",
        ["log_retention_days"] = "30"
    };

    // ---- TimescaleDB ----

    [HttpGet("timescaledb")]
    public IActionResult GetTimeScaleDb()
    {
        var cfg = Load();
        return Ok(new
        {
            host = cfg["timescaledb_host"],
            port = cfg["timescaledb_port"],
            database = cfg["timescaledb_database"],
            username = cfg["timescaledb_username"]
        });
    }

    [HttpPut("timescaledb")]
    public IActionResult SetTimeScaleDb([FromBody] Dictionary<string, string> body)
    {
        var cfg = Load();
        foreach (var (k, v) in body) cfg[$"timescaledb_{k}"] = v;
        Save(cfg);
        return Ok(new { message = "时序库配置已保存" });
    }

    // ---- Relational DB ----

    [HttpGet("relational")]
    public IActionResult GetRelational()
    {
        var cfg = Load();
        return Ok(new
        {
            host = cfg["relational_host"],
            port = cfg["relational_port"],
            database = cfg["relational_database"],
            username = cfg["relational_username"]
        });
    }

    [HttpPut("relational")]
    public IActionResult SetRelational([FromBody] Dictionary<string, string> body)
    {
        var cfg = Load();
        foreach (var (k, v) in body) cfg[$"relational_{k}"] = v;
        Save(cfg);
        return Ok(new { message = "关系库配置已保存" });
    }

    // ---- System (returns ints for numeric fields — TAG: type-fix) ----

    [HttpGet("system")]
    public IActionResult GetSystem()
    {
        var cfg = Load();
        return Ok(new
        {
            collectInterval = ParseInt(cfg, "collect_interval", 1000),
            timeout = ParseInt(cfg, "timeout", 3000),
            reconnectInterval = ParseInt(cfg, "reconnect_interval", 10),
            logRetentionDays = ParseInt(cfg, "log_retention_days", 30)
        });
    }

    [HttpPut("system")]
    public IActionResult SetSystem([FromBody] Dictionary<string, string> body)
    {
        var cfg = Load();
        foreach (var (k, v) in body) cfg[k] = v;
        Save(cfg);
        return Ok(new { message = "系统设置已保存" });
    }

    private static int ParseInt(Dictionary<string, string> cfg, string key, int fallback)
    {
        return int.TryParse(cfg.GetValueOrDefault(key, ""), out var v) ? v : fallback;
    }
}
