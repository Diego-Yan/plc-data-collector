// TAG: review-fix-2 — 2026-06-22 — int deviceId/pointId instead of string
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
        var points = _deviceManager.GetDevicePoints(deviceId);
        return Ok(points);
    }

    [HttpPost("devices/{deviceId}/points")]
    public async Task<IActionResult> CreatePoint(int deviceId, [FromBody] Point point)
    {
        if (string.IsNullOrWhiteSpace(point.Code))
            return BadRequest(new { error = "点位编码不能为空" });
        if (string.IsNullOrWhiteSpace(point.Address))
            return BadRequest(new { error = "点位地址不能为空" });

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

    [HttpPost("devices/{deviceId}/points/batch")]
    public async Task<IActionResult> BatchImport(int deviceId, [FromBody] List<Point> points)
    {
        if (points.Count == 0)
            return BadRequest(new { error = "导入列表不能为空" });

        foreach (var point in points)
            point.DeviceId = deviceId;

        var count = await _deviceManager.AddPointsBatch(points);
        return Ok(new { imported = count });
    }

    [HttpPost("points/batch-delete")]
    public async Task<IActionResult> BatchDelete([FromBody] List<int> ids)
    {
        if (ids.Count == 0)
            return BadRequest(new { error = "删除列表不能为空" });

        await _deviceManager.BatchDeletePoints(ids);
        return NoContent();
    }
}
