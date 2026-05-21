using System.Collections.Generic;
using PLCDataCollector.Core.Models;

namespace PLCDataCollector.Core.Plc;

public class CollectTask
{
    public string DeviceId { get; set; } = "";
    public List<CollectPoint> Points { get; set; } = new();
}

public class CollectPoint
{
    public string PointId { get; set; } = "";
    public string Address { get; set; } = "";
    public DataType DataType { get; set; }
}
