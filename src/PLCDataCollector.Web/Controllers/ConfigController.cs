// ============================================================================
// TAG: rewritten — 2026-05-20 — JSON file persistence, thread-safe, typed system config
// TAG: review-fix-3 — 2026-06-22 — SetSystem accepts camelCase keys from frontend,
//      maps them to snake_case config keys internally.
// ============================================================================

using Microsoft.AspNetCore.Mvc;

namespace PLCDataCollector.Web.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController : ControllerBase
{
    private static readonly string ConfigDir = Path.Combine(AppContext.BaseDirectory, "config");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "system_config.json");
    private static readonly object _fileLock = new();

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
                    var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loaded != null)
                    {
                        var merged = Defaults();
                        foreach (var (k, v) in loaded)
                            merged[k] = v;
                        return merged;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Config load failed: " + ex.Message);
            }
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
                System.IO.File.WriteAllText(ConfigPath,
                    System.Text.Json.JsonSerializer.Serialize(cfg));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Config save failed: " + ex.Message);
            }
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
            host = cfg.GetValueOrDefault("timescaledb_host", "127.0.0.1"),
            port = cfg.GetValueOrDefault("timescaledb_port", "5432"),
            database = cfg.GetValueOrDefault("timescaledb_database", "plc_data"),
            username = cfg.GetValueOrDefault("timescaledb_username", "postgres")
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
            host = cfg.GetValueOrDefault("relational_host", "127.0.0.1"),
            port = cfg.GetValueOrDefault("relational_port", "5432"),
            database = cfg.GetValueOrDefault("relational_database", "plc_data_forward"),
            username = cfg.GetValueOrDefault("relational_username", "postgres")
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

    // ---- System ----

    private static readonly Dictionary<string, string> SystemKeyMap = new()
    {
        ["collectInterval"] = "collect_interval",
        ["timeout"] = "timeout",
        ["reconnectInterval"] = "reconnect_interval",
        ["logRetentionDays"] = "log_retention_days"
    };

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
        foreach (var (k, v) in body)
        {
            if (!SystemKeyMap.TryGetValue(k, out var configKey))
                return BadRequest(new { error = $"Unknown system key: {k}" });
            cfg[configKey] = v;
        }
        Save(cfg);
        return Ok(new { message = "系统设置已保存" });
    }

    private static int ParseInt(Dictionary<string, string> cfg, string key, int fallback)
    {
        return int.TryParse(cfg.GetValueOrDefault(key, ""), out var v) ? v : fallback;
    }
}
