// ============================================================================
// TAG: review-fix — 2026-06-22 — existence check on update, input validation,
//      UpdateAsync returns bool for 404.
// ============================================================================

using Microsoft.AspNetCore.Mvc;
using PLCDataCollector.Core.Models;
using PLCDataCollector.Web.Services;

namespace PLCDataCollector.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DevicesController : ControllerBase
{
    private readonly DeviceManager _deviceManager;

    public DevicesController(DeviceManager deviceManager)
    {
        _deviceManager = deviceManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var devices = await _deviceManager.GetAllAsync();
        var total = devices.Count;
        var items = devices.Skip((page - 1) * size).Take(size).ToList();
        return Ok(new { total, items });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var device = await _deviceManager.GetByIdAsync(id);
        return device != null ? Ok(device) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Device device)
    {
        try
        {
            var id = await _deviceManager.CreateAsync(device);
            device.Id = id;
            return CreatedAtAction(nameof(Get), new { id }, device);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Device device)
    {
        device.Id = id;
        var ok = await _deviceManager.UpdateAsync(device);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _deviceManager.DeleteAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/reconnect")]
    public async Task<IActionResult> Reconnect(int id)
    {
        await _deviceManager.ReconnectAsync(id);
        return Ok(new { message = "重连已触发" });
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> SetStatus(int id, [FromBody] SetStatusRequest request)
    {
        await _deviceManager.SetStatusAsync(id, request.Enabled);
        return NoContent();
    }
}

public class SetStatusRequest
{
    public bool Enabled { get; set; }
}
