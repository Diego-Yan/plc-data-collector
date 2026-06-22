using System;

namespace PLCDataCollector.Core.Models;

public class Device
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string IpAddress { get; set; } = "";
    public int Port { get; set; } = 102;
    public int Rack { get; set; } = 0;
    public int Slot { get; set; } = 1;
    public string Protocol { get; set; } = "S7";
    public bool Enabled { get; set; } = true;
    public bool IsOnline { get; set; }
    public DateTime? LastCollectedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Device Copy() => new()
    {
        Id = Id,
        Name = Name,
        IpAddress = IpAddress,
        Port = Port,
        Rack = Rack,
        Slot = Slot,
        Protocol = Protocol,
        Enabled = Enabled,
        IsOnline = IsOnline,
        LastCollectedAt = LastCollectedAt,
        CreatedAt = CreatedAt
    };
}

public class Point
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public DataType DataType { get; set; } = DataType.Real;
    public string Unit { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public enum DataType
{
    Bool, Byte, Word, DWord, Int, DInt, Real
}

public class PointValue
{
    public int DeviceId { get; set; }
    public int PointId { get; set; }
    public double? Value { get; set; }
    public DateTime Timestamp { get; set; }
    public QualityStatus Quality { get; set; } = QualityStatus.Good;
}

public enum QualityStatus
{
    Good = 0,
    Timeout = 1,
    BadValue = 2,
    DeviceOffline = 3,
    NotCollected = 4
}
