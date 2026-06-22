// ============================================================================
// TAG: merged-from-service — 2026-05-20
// TAG: thread-safe — 2026-05-20 — added ReaderWriterLockSlim to protect
// TAG: review-fix — 2026-06-22 — batch point operations, UpdateOnlineStatus,
//      input validation, save outside lock.
// TAG: review-fix-2 — 2026-06-22 — GetDevicePoints overloads with int, remove
//      string-based overloads. Debounce UpdateOnlineStatus save (only persist
//      on shutdown, not every 1s collection cycle).
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using PLCDataCollector.Core.Models;

namespace PLCDataCollector.Web.Services;

public class DeviceManager : IDisposable
{
    private readonly List<Device> _devices = new();
    private readonly List<Point> _points = new();
    private int _nextDeviceId = 1;
    private int _nextPointId = 1;
    private readonly ReaderWriterLockSlim _rwLock = new();
    private readonly string _dataDir;
    private bool _dirty;

    private static readonly Regex IpRegex = new(
        @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.Compiled);

    public DeviceManager()
    {
        _dataDir = Path.Combine(AppContext.BaseDirectory, "config");
        Directory.CreateDirectory(_dataDir);
        Load();
    }

    private string DevicesPath => Path.Combine(_dataDir, "devices.json");
    private string PointsPath => Path.Combine(_dataDir, "points.json");

    private void Load()
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (File.Exists(DevicesPath))
            {
                var json = File.ReadAllText(DevicesPath);
                var loaded = JsonSerializer.Deserialize<List<Device>>(json);
                if (loaded != null) { _devices.Clear(); _devices.AddRange(loaded); }
            }
            if (File.Exists(PointsPath))
            {
                var json = File.ReadAllText(PointsPath);
                var loaded = JsonSerializer.Deserialize<List<Point>>(json);
                if (loaded != null) { _points.Clear(); _points.AddRange(loaded); }
            }
            _nextDeviceId = _devices.Count > 0 ? _devices.Max(d => d.Id) + 1 : 1;
            _nextPointId = _points.Count > 0 ? _points.Max(p => p.Id) + 1 : 1;
            _dirty = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("DeviceManager load failed: " + ex.Message);
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    private void Save()
    {
        string devicesJson;
        string pointsJson;

        _rwLock.EnterReadLock();
        try
        {
            devicesJson = JsonSerializer.Serialize(_devices);
            pointsJson = JsonSerializer.Serialize(_points);
        }
        finally { _rwLock.ExitReadLock(); }

        try
        {
            File.WriteAllText(DevicesPath, devicesJson);
            File.WriteAllText(PointsPath, pointsJson);
            _dirty = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("DeviceManager save failed: " + ex.Message);
        }
    }

    public void Flush()
    {
        if (_dirty) Save();
    }

    private static void ValidateDevice(Device device)
    {
        if (string.IsNullOrWhiteSpace(device.Name))
            throw new ArgumentException("设备名称不能为空");
        if (string.IsNullOrWhiteSpace(device.IpAddress) || !IpRegex.IsMatch(device.IpAddress))
            throw new ArgumentException("无效的IP地址");
        if (device.Port < 1 || device.Port > 65535)
            throw new ArgumentException("端口必须在1-65535之间");
    }

    // ---- device CRUD ----

    public Task<List<Device>> GetAllAsync()
    {
        _rwLock.EnterReadLock();
        try { return Task.FromResult(_devices.ToList()); }
        finally { _rwLock.ExitReadLock(); }
    }

    public Task<Device?> GetByIdAsync(int id)
    {
        _rwLock.EnterReadLock();
        try { return Task.FromResult(_devices.FirstOrDefault(d => d.Id == id)); }
        finally { _rwLock.ExitReadLock(); }
    }

    public Task<int> CreateAsync(Device device)
    {
        ValidateDevice(device);
        _rwLock.EnterWriteLock();
        try
        {
            device.Id = _nextDeviceId++;
            device.CreatedAt = DateTime.UtcNow;
            _devices.Add(device);
            _dirty = true;
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.FromResult(device.Id);
    }

    public Task<bool> UpdateAsync(Device device)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var idx = _devices.FindIndex(d => d.Id == device.Id);
            if (idx < 0) return Task.FromResult(false);
            _devices[idx] = device;
            _dirty = true;
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.FromResult(true);
    }

    public Task DeleteAsync(int id)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _devices.RemoveAll(d => d.Id == id);
            _points.RemoveAll(p => p.DeviceId == id);
            _dirty = true;
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.CompletedTask;
    }

    public Task ReconnectAsync(int id)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var device = _devices.FirstOrDefault(d => d.Id == id);
            if (device != null) { device.IsOnline = false; _dirty = true; }
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.CompletedTask;
    }

    public Task SetStatusAsync(int id, bool enabled)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var device = _devices.FirstOrDefault(d => d.Id == id);
            if (device != null) { device.Enabled = enabled; _dirty = true; }
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.CompletedTask;
    }

    public void UpdateOnlineStatus(int id, bool online)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var device = _devices.FirstOrDefault(d => d.Id == id);
            if (device != null)
            {
                device.IsOnline = online;
                if (online)
                    device.LastCollectedAt = DateTime.UtcNow;
                _dirty = true;
            }
        }
        finally { _rwLock.ExitWriteLock(); }
        // NOTE: Save is deferred — called at shutdown via Flush() to avoid
        // disk I/O every 1s collection cycle. Use Flush() to persist on demand.
    }

    // ---- query helpers ----

    public List<Device> GetActiveDevices()
    {
        _rwLock.EnterReadLock();
        try { return _devices.Where(d => d.Enabled).Select(d => d.Copy()).ToList(); }
        finally { _rwLock.ExitReadLock(); }
    }

    public List<Point> GetDevicePoints(int deviceId)
    {
        _rwLock.EnterReadLock();
        try { return _points.Where(p => p.DeviceId == deviceId).ToList(); }
        finally { _rwLock.ExitReadLock(); }
    }

    public Point? GetPointById(int pointId)
    {
        _rwLock.EnterReadLock();
        try { return _points.FirstOrDefault(p => p.Id == pointId); }
        finally { _rwLock.ExitReadLock(); }
    }

    public List<Point> GetAllPoints()
    {
        _rwLock.EnterReadLock();
        try { return _points.ToList(); }
        finally { _rwLock.ExitReadLock(); }
    }

    // ---- point CRUD ----

    public Task<int> AddPoint(Point point)
    {
        _rwLock.EnterWriteLock();
        try
        {
            point.Id = _nextPointId++;
            _points.Add(point);
            _dirty = true;
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.FromResult(point.Id);
    }

    public Task<int> AddPointsBatch(List<Point> points)
    {
        var ids = new List<int>();
        _rwLock.EnterWriteLock();
        try
        {
            foreach (var point in points)
            {
                point.Id = _nextPointId++;
                _points.Add(point);
                ids.Add(point.Id);
            }
            _dirty = true;
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.FromResult(ids.Count);
    }

    public Task UpdatePoint(Point point)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var existing = _points.FirstOrDefault(p => p.Id == point.Id);
            if (existing != null)
            {
                point.DeviceId = existing.DeviceId;
                var idx = _points.FindIndex(p => p.Id == point.Id);
                if (idx >= 0) _points[idx] = point;
                _dirty = true;
            }
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.CompletedTask;
    }

    public Task DeletePoint(int id)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _points.RemoveAll(p => p.Id == id);
            _dirty = true;
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.CompletedTask;
    }

    public Task BatchDeletePoints(List<int> ids)
    {
        var idSet = new HashSet<int>(ids);
        _rwLock.EnterWriteLock();
        try
        {
            _points.RemoveAll(p => idSet.Contains(p.Id));
            _dirty = true;
        }
        finally { _rwLock.ExitWriteLock(); }
        Save();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Flush();
        _rwLock.Dispose();
    }
}
