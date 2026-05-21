// ============================================================================
// TAG: merged-from-service — 2026-05-20
// TAG: thread-safe — 2026-05-20 — added ReaderWriterLockSlim to protect
//   _devices/_points from concurrent access by CollectorService, ForwardService,
//   and HTTP controllers.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

    public DeviceManager()
    {
        _dataDir = Path.Combine(AppContext.BaseDirectory, "config");
        Directory.CreateDirectory(_dataDir);
        Load();
    }

    // ---- persistence ----

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
        }
        catch { /* corrupt file — start fresh */ }
        finally { _rwLock.ExitWriteLock(); }
    }

    private void Save()
    {
        // caller must hold write lock; file I/O inside lock is acceptable for small JSON payloads
        try
        {
            File.WriteAllText(DevicesPath, JsonSerializer.Serialize(_devices));
            File.WriteAllText(PointsPath, JsonSerializer.Serialize(_points));
        }
        catch { /* best-effort */ }
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
        _rwLock.EnterWriteLock();
        try
        {
            device.Id = _nextDeviceId++;
            device.CreatedAt = DateTime.UtcNow;
            _devices.Add(device);
            Save();
            return Task.FromResult(device.Id);
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task UpdateAsync(Device device)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var idx = _devices.FindIndex(d => d.Id == device.Id);
            if (idx >= 0) _devices[idx] = device;
            Save();
            return Task.CompletedTask;
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task DeleteAsync(int id)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _devices.RemoveAll(d => d.Id == id);
            _points.RemoveAll(p => p.DeviceId == id);
            Save();
            return Task.CompletedTask;
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task ReconnectAsync(int id)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var device = _devices.FirstOrDefault(d => d.Id == id);
            if (device != null) device.IsOnline = false;
            // note: does not close the actual PlcConnection — see CollectorService for full reconnect
            return Task.CompletedTask;
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task SetStatusAsync(int id, bool enabled)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var device = _devices.FirstOrDefault(d => d.Id == id);
            if (device != null) device.Enabled = enabled;
            Save();
            return Task.CompletedTask;
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    // ---- query helpers (used by CollectorService + ForwardService) ----

    // TAG: thread-safe — returns shallow copies so CollectorService can mutate without lock
    public List<Device> GetActiveDevices()
    {
        _rwLock.EnterReadLock();
        try { return _devices.Where(d => d.Enabled).Select(d => d.Copy()).ToList(); }
        finally { _rwLock.ExitReadLock(); }
    }

    public List<Point> GetDevicePoints(string deviceIdStr)
    {
        if (!int.TryParse(deviceIdStr, out var deviceId)) return new();
        _rwLock.EnterReadLock();
        try { return _points.Where(p => p.DeviceId == deviceId).ToList(); }
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
            Save();
            return Task.FromResult(point.Id);
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task UpdatePoint(Point point)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var idx = _points.FindIndex(p => p.Id == point.Id);
            if (idx >= 0) _points[idx] = point;
            Save();
            return Task.CompletedTask;
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public Task DeletePoint(int id)
    {
        _rwLock.EnterWriteLock();
        try
        {
            _points.RemoveAll(p => p.Id == id);
            Save();
            return Task.CompletedTask;
        }
        finally { _rwLock.ExitWriteLock(); }
    }

    public void Dispose()
    {
        _rwLock.Dispose();
    }
}
