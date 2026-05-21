// ============================================================================
// TAG: fixed — 2026-05-20
// Added [HttpPost("devices/{deviceId}/points/batch")] route to match frontend
// API call (was /api/points/batch, frontend sends /api/devices/{id}/points/batch).
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using PLCDataCollector.Core.Models;
using PLCDataCollector.Web.Services;

namespace PLCDataCollector.Web.Controllers;

[ApiController]
[Route("api")]
public class PointsController : ControllerBase
{
    private readonly DeviceManager _deviceManager;

    public PointsController(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    [HttpGet("devices/{deviceId}/points")]
    public IActionResult GetPoints(int deviceId)
    {
        var points = _deviceManager.GetDevicePoints(deviceId.ToString());
        return Ok(points);
    }

    [HttpPost("devices/{deviceId}/points")]
    public async Task<IActionResult> CreatePoint(int deviceId, [FromBody] Point point)
    {
        point.DeviceId = deviceId;
        var id = await _deviceManager.AddPoint(point);
        point.Id = id;
        return CreatedAtAction(nameof(GetPoints), new { deviceId }, point);
    }

    [HttpPut("points/{id}")]
    public async Task<IActionResult> UpdatePoint(int id, [FromBody] Point point)
    {
        point.Id = id;
        await _deviceManager.UpdatePoint(point);
        return NoContent();
    }

    [HttpDelete("points/{id}")]
    public async Task<IActionResult> DeletePoint(int id)
    {
        await _deviceManager.DeletePoint(id);
        return NoContent();
    }

    // TAG: fixed — route now matches frontend api/index.ts batchImport
    [HttpPost("devices/{deviceId}/points/batch")]
    public async Task<IActionResult> BatchImport(int deviceId, [FromBody] List<Point> points)
    {
        foreach (var point in points)
        {
            point.DeviceId = deviceId;
            await _deviceManager.AddPoint(point);
        }
        return Ok(new { imported = points.Count });
    }

    [HttpPost("points/batch-delete")]
    public async Task<IActionResult> BatchDelete([FromBody] List<int> ids)
    {
        foreach (var id in ids)
            await _deviceManager.DeletePoint(id);
        return NoContent();
    }
}
