using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using IoTDetectorApi.Data;

namespace IoTDetectorApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExportController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExportController> _logger;

    public ExportController(ApplicationDbContext context, ILogger<ExportController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("telemetry/csv")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ExportTelemetryToCsv(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int limit = 10000)
    {
        // Validate and cap limit to prevent excessive exports
        if (limit < 1) limit = 1000;
        if (limit > 100000) limit = 100000;

        _logger.LogInformation(
            "Exporting telemetry to CSV - startDate: {StartDate}, endDate: {EndDate}, deviceId: {DeviceId}, limit: {Limit}",
            startDate, endDate, deviceId ?? "all", limit);

        var query = _context.SensorData.AsNoTracking();

        // Apply filters using indexes
        if (startDate.HasValue)
            query = query.Where(s => s.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(s => s.Timestamp <= endDate.Value);

        if (!string.IsNullOrEmpty(deviceId))
            query = query.Where(s => s.DeviceId == deviceId);

        var data = await query
            .OrderByDescending(s => s.Timestamp)
            .Take(limit)
            .ToListAsync();

        _logger.LogInformation("Exporting {Count} telemetry records to CSV", data.Count);

        var csv = new StringBuilder();

        // Add UTF-8 BOM for Excel compatibility
        csv.Append('\uFEFF');

        // Header
        csv.AppendLine("ID,DeviceID,EventType,Value,Timestamp,RawData");

        // Data rows with proper CSV escaping
        foreach (var record in data.OrderBy(s => s.Timestamp))
        {
            csv.AppendLine(string.Join(",",
                EscapeCsvField(record.Id.ToString()),
                EscapeCsvField(record.DeviceId),
                EscapeCsvField(record.EventType),
                EscapeCsvField(record.Value?.ToString() ?? ""),
                EscapeCsvField(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsvField(record.RawData ?? "")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"telemetry_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    private static string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "\"\"";

        // If field contains comma, quote, or newline, wrap in quotes and escape quotes
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }

    [HttpGet("telemetry/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<object>> ExportTelemetryToJson(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int limit = 10000)
    {
        // Validate and cap limit
        if (limit < 1) limit = 1000;
        if (limit > 100000) limit = 100000;

        _logger.LogInformation(
            "Exporting telemetry to JSON - startDate: {StartDate}, endDate: {EndDate}, deviceId: {DeviceId}, limit: {Limit}",
            startDate, endDate, deviceId ?? "all", limit);

        var query = _context.SensorData.AsNoTracking();

        if (startDate.HasValue)
            query = query.Where(s => s.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(s => s.Timestamp <= endDate.Value);

        if (!string.IsNullOrEmpty(deviceId))
            query = query.Where(s => s.DeviceId == deviceId);

        var data = await query
            .OrderByDescending(s => s.Timestamp)
            .Take(limit)
            .Select(s => new
            {
                s.Id,
                s.DeviceId,
                s.EventType,
                s.Value,
                s.Timestamp,
                s.RawData
            })
            .ToListAsync();

        _logger.LogInformation("Exported {Count} telemetry records to JSON", data.Count);

        return Ok(new
        {
            exportDate = DateTime.UtcNow,
            totalRecords = data.Count,
            filters = new
            {
                startDate,
                endDate,
                deviceId,
                limit
            },
            data = data.OrderBy(s => s.Timestamp)
        });
    }

    [HttpGet("events/csv")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ExportEventsToCsv(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? level = null,
        [FromQuery] int limit = 10000)
    {
        // Validate and cap limit
        if (limit < 1) limit = 1000;
        if (limit > 100000) limit = 100000;

        _logger.LogInformation(
            "Exporting events to CSV - startDate: {StartDate}, endDate: {EndDate}, level: {Level}, limit: {Limit}",
            startDate, endDate, level ?? "all", limit);

        var query = _context.EventLogs.AsNoTracking();

        // Apply filters using indexes
        if (startDate.HasValue)
            query = query.Where(e => e.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Timestamp <= endDate.Value);

        if (!string.IsNullOrEmpty(level))
            query = query.Where(e => e.Level == level);

        var data = await query
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync();

        _logger.LogInformation("Exporting {Count} event records to CSV", data.Count);

        var csv = new StringBuilder();

        // Add UTF-8 BOM for Excel compatibility
        csv.Append('\uFEFF');

        // Header
        csv.AppendLine("ID,Timestamp,Level,Message,DeviceID");

        // Data rows with proper CSV escaping
        foreach (var record in data.OrderBy(e => e.Timestamp))
        {
            csv.AppendLine(string.Join(",",
                EscapeCsvField(record.Id.ToString()),
                EscapeCsvField(record.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")),
                EscapeCsvField(record.Level),
                EscapeCsvField(record.Message),
                EscapeCsvField(record.DeviceId ?? "")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"events_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
    }

    [HttpGet("events/json")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<object>> ExportEventsToJson(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? level = null,
        [FromQuery] int limit = 10000)
    {
        // Validate and cap limit
        if (limit < 1) limit = 1000;
        if (limit > 100000) limit = 100000;

        _logger.LogInformation(
            "Exporting events to JSON - startDate: {StartDate}, endDate: {EndDate}, level: {Level}, limit: {Limit}",
            startDate, endDate, level ?? "all", limit);

        var query = _context.EventLogs.AsNoTracking();

        if (startDate.HasValue)
            query = query.Where(e => e.Timestamp >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(e => e.Timestamp <= endDate.Value);

        if (!string.IsNullOrEmpty(level))
            query = query.Where(e => e.Level == level);

        var data = await query
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .Select(e => new
            {
                e.Id,
                e.Timestamp,
                e.Level,
                e.Message,
                e.DeviceId
            })
            .ToListAsync();

        _logger.LogInformation("Exported {Count} event records to JSON", data.Count);

        return Ok(new
        {
            exportDate = DateTime.UtcNow,
            totalRecords = data.Count,
            filters = new
            {
                startDate,
                endDate,
                level,
                limit
            },
            data = data.OrderBy(e => e.Timestamp)
        });
    }
}
