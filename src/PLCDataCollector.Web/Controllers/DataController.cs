using Microsoft.AspNetCore.Mvc;
using PLCDataCollector.Core.Cache;
using PLCDataCollector.Core.Models;
using PLCDataCollector.Core.Storage;
// TAG: fixed — namespace updated after single-process merge
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
        var deviceIdStr = deviceId.ToString();
        var points = _deviceManager.GetDevicePoints(deviceIdStr);
        var results = new List<PointValue>();

        foreach (var point in points)
        {
            var pv = await _cache.GetPointValue(deviceIdStr, point.Id.ToString());
            if (pv != null) results.Add(pv);
        }

        return Ok(results);
    }

    [HttpGet("points/{id}/realtime")]
    public async Task<IActionResult> GetPointRealtime(int id)
    {
        var points = _deviceManager.GetAllPoints();
        var point = points.FirstOrDefault(p => p.Id == id);
        if (point == null) return NotFound();

        var pv = await _cache.GetPointValue(point.DeviceId.ToString(), id.ToString());
        return Ok(pv ?? new PointValue());
    }

    [HttpGet("devices/{deviceId}/history")]
    public async Task<IActionResult> GetHistory(
        int deviceId,
        [FromQuery] string? pointIds,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var ids = (pointIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length == 0) return Ok(new List<PointValue>());

        var fromDate = from ?? DateTime.UtcNow.AddHours(-1);
        var toDate = to ?? DateTime.UtcNow;

        var results = await _tsService.QueryHistoryAsync(deviceId.ToString(), ids, fromDate, toDate);
        return Ok(results);
    }
}
