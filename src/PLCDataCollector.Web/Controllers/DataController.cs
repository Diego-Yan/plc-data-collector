using Microsoft.AspNetCore.Mvc;
using PLCDataCollector.Core.Cache;
using PLCDataCollector.Core.Models;
using PLCDataCollector.Core.Storage;
using PLCDataCollector.Web.Services;

namespace PLCDataCollector.Web.Controllers;

[ApiController]
[Route("api")]
public class DataController : ControllerBase
{
    private readonly MemoryCacheService _cache;
    private readonly TimeSeriesService _tsService;
    private readonly DeviceManager _deviceManager;

    public DataController(MemoryCacheService cache, TimeSeriesService tsService, DeviceManager deviceManager)
    {
        _cache = cache;
        _tsService = tsService;
        _deviceManager = deviceManager;
    }

    [HttpGet("devices/{deviceId}/realtime")]
    public async Task<IActionResult> GetDeviceRealtime(int deviceId)
    {
        var device = await _deviceManager.GetByIdAsync(deviceId);
        if (device == null) return NotFound();

        var points = _deviceManager.GetDevicePoints(deviceId);
        var results = new List<PointValue>();

        foreach (var point in points)
        {
            var pv = await _cache.GetPointValue(deviceId, point.Id);
            if (pv != null) results.Add(pv);
        }

        return Ok(results);
    }

    [HttpGet("points/{id}/realtime")]
    public async Task<IActionResult> GetPointRealtime(int id)
    {
        var point = _deviceManager.GetPointById(id);
        if (point == null) return NotFound();

        var pv = await _cache.GetPointValue(point.DeviceId, id);
        return Ok(pv ?? new PointValue());
    }

    [HttpGet("devices/{deviceId}/history")]
    public async Task<IActionResult> GetHistory(
        int deviceId,
        [FromQuery] string? pointIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var ids = (pointIds ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => int.TryParse(s, out _))
            .Select(int.Parse)
            .ToArray();

        if (ids.Length == 0) return Ok(new List<PointValue>());

        var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
        var toDate = to ?? DateTime.UtcNow;

        if (fromDate > toDate)
        {
            (fromDate, toDate) = (toDate, fromDate);
        }

        var results = await _tsService.QueryHistoryAsync(deviceId, ids, fromDate, toDate);
        return Ok(results);
    }
}
