// TAG: fixed — removed unused using System.Collections.Generic
using System;

namespace PLCDataCollector.Core.Configuration;

// TAG: removed RedisConfig — replaced by process-internal MemoryCache
public class AppConfig
{
    public CollectorConfig Collector { get; set; } = new();
    public DatabaseConfig TimeSeriesDb { get; set; } = new();
    public DatabaseConfig RelationalDb { get; set; } = new();
    public ForwardConfig Forward { get; set; } = new();
}

public class CollectorConfig
{
    public int CollectIntervalMs { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 3000;
    public int ReconnectIntervalSec { get; set; } = 10;
    public int ServiceDelayStartSec { get; set; } = 5;
}

public class DatabaseConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "plc_data";
    public string Username { get; set; } = "postgres";
    public string Password { get; set; } = "";
}

public class ForwardConfig
{
    public int IntervalSec { get; set; } = 10;
}
