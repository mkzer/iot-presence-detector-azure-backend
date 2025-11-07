using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IoTDetectorApi.Data;

namespace IoTDetectorApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TelemetryController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(ApplicationDbContext context, ILogger<TelemetryController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("activity")]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "hours", "deviceId" })]
    public async Task<ActionResult<object>> GetActivityData([FromQuery] int hours = 24, [FromQuery] string? deviceId = null)
    {
        _logger.LogInformation("Fetching activity data for last {Hours} hours, deviceId: {DeviceId}", hours, deviceId ?? "all");

        var startTime = DateTime.UtcNow.AddHours(-hours);

        // Build query to use composite index IX_SensorData_DeviceId_Timestamp when deviceId is specified
        var query = _context.SensorData.AsNoTracking();

        // Apply filters in optimal order for index usage
        if (!string.IsNullOrEmpty(deviceId))
        {
            // Use composite index: IX_SensorData_DeviceId_Timestamp
            query = query.Where(s => s.DeviceId == deviceId && s.Timestamp >= startTime);
        }
        else
        {
            // Use index: IX_SensorData_Timestamp
            query = query.Where(s => s.Timestamp >= startTime);
        }

        // Filter by event type (uses IX_SensorData_EventType)
        query = query.Where(s => s.EventType == "motion" || s.EventType == "motion_detected");

        // Order by timestamp ascending for grouping
        var data = await query
            .OrderBy(s => s.Timestamp)
            .Select(s => new { s.Timestamp, s.DeviceId })
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} sensor data records", data.Count);

        // Group by hour in memory (minimal data after projection)
        var grouped = data
            .GroupBy(s => new DateTime(s.Timestamp.Year, s.Timestamp.Month, s.Timestamp.Day, s.Timestamp.Hour, 0, 0))
            .Select(g => new
            {
                time = g.Key.ToString("dd/MM HH:mm"),
                value = g.Count(),
                deviceId = deviceId ?? "all"
            })
            .ToList();

        return Ok(grouped);
    }

    [HttpGet("recent")]
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "limit" })]
    public async Task<ActionResult<object>> GetRecentData([FromQuery] int limit = 100)
    {
        // Validate and cap limit to prevent excessive data loading
        if (limit < 1) limit = 10;
        if (limit > 1000) limit = 1000;

        _logger.LogInformation("Fetching {Limit} most recent sensor data records", limit);

        // Uses descending index IX_SensorData_Timestamp for optimal performance
        var data = await _context.SensorData
            .AsNoTracking()
            .OrderByDescending(s => s.Timestamp)
            .Take(limit)
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} recent sensor data records", data.Count);

        return Ok(data);
    }
}
