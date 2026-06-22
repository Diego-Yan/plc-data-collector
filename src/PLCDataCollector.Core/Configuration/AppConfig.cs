namespace PLCDataCollector.Core.Configuration;

public class AppConfig
{
    public CollectorConfig Collector { get; set; } = new();
    public ForwardConfig Forward { get; set; } = new();
}

public class CollectorConfig
{
    public int CollectIntervalMs { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 3000;
    public int ReconnectIntervalSec { get; set; } = 10;
    public int ServiceDelayStartSec { get; set; } = 5;
}

public class ForwardConfig
{
    public int IntervalSec { get; set; } = 10;
}
