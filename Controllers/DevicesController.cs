using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IoTDetectorApi.Data;
using IoTDetectorApi.Models;

namespace IoTDetectorApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(ApplicationDbContext context, ILogger<DevicesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "page", "pageSize", "status", "type", "search" })]
    public async Task<ActionResult<object>> GetDevices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null)
    {
        _logger.LogInformation("Fetching devices - page: {Page}, pageSize: {PageSize}, status: {Status}, type: {Type}, search: {Search}",
            page, pageSize, status ?? "all", type ?? "all", search ?? "none");

        // Validate pagination parameters
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 50;
        if (pageSize > 100) pageSize = 100; // Max 100 items per page

        // Build query with filters - AsNoTracking for read-only queries
        var query = _context.Devices.AsNoTracking();

        // Apply indexed filters first for better performance
        if (!string.IsNullOrWhiteSpace(status))
        {
            // Uses index: IX_Devices_Status
            query = query.Where(d => d.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            // Uses index: IX_Devices_Type
            query = query.Where(d => d.Type == type);
        }

        // Apply search filter (less efficient, applied after indexed filters)
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLower();
            query = query.Where(d =>
                d.Id.ToLower().Contains(searchLower) ||
                d.Name.ToLower().Contains(searchLower));
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply pagination - Uses descending index IX_Devices_LastSeen
        var devices = await query
            .OrderByDescending(d => d.LastSeen)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Update last signal text
        foreach (var device in devices)
        {
            if (device.LastSeen.HasValue)
            {
                var timeAgo = DateTime.UtcNow - device.LastSeen.Value;
                device.LastSignal = timeAgo.TotalMinutes < 1
                    ? "Il y a moins d'1 min"
                    : timeAgo.TotalMinutes < 60
                        ? $"Il y a {(int)timeAgo.TotalMinutes} min"
                        : timeAgo.TotalHours < 24
                            ? $"Il y a {(int)timeAgo.TotalHours} h"
                            : $"Il y a {(int)timeAgo.TotalDays} j";
            }
            else
            {
                device.LastSignal = "Jamais vu";
            }
        }

        _logger.LogInformation("Returning {Count} devices out of {Total}", devices.Count, totalCount);

        return Ok(new
        {
            data = devices,
            pagination = new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                hasNextPage = page * pageSize < totalCount,
                hasPreviousPage = page > 1
            }
        });
    }

    [HttpGet("{id}")]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "id" })]
    public async Task<ActionResult<Device>> GetDevice(string id)
    {
        _logger.LogInformation("Fetching device by ID: {DeviceId}", id);

        // Use AsNoTracking for read-only query
        var device = await _context.Devices
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (device == null)
        {
            _logger.LogWarning("Device not found: {DeviceId}", id);
            return NotFound();
        }

        return Ok(device);
    }

    [HttpGet("stats")]
    [ResponseCache(Duration = 30)]
    public async Task<ActionResult<object>> GetStats()
    {
        _logger.LogInformation("Fetching device statistics");

        // Use AsNoTracking for all read-only queries
        var totalDevices = await _context.Devices.AsNoTracking().CountAsync();

        // Uses index: IX_Devices_Status
        var activeDevices = await _context.Devices
            .AsNoTracking()
            .CountAsync(d => d.Status == "active");

        var today = DateTime.UtcNow.Date;

        // Uses index: IX_SensorData_Timestamp
        var eventsToday = await _context.SensorData
            .AsNoTracking()
            .CountAsync(s => s.Timestamp >= today);

        // Uses index: IX_EventLogs_Level
        var activeAlerts = await _context.EventLogs
            .AsNoTracking()
            .CountAsync(e => e.Level == "warning" || e.Level == "error");

        // Uses index: IX_Devices_Type
        var deviceTypes = await _context.Devices
            .AsNoTracking()
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        var typeBreakdown = string.Join(", ", deviceTypes.Select(t => $"{t.Count} {t.Type}"));

        var stats = new
        {
            totalDevices,
            activeDevices,
            eventsToday,
            activeAlerts,
            availability = totalDevices > 0 ? (int)((double)activeDevices / totalDevices * 100) : 0,
            deviceTypeBreakdown = typeBreakdown
        };

        _logger.LogInformation("Stats: {TotalDevices} total, {ActiveDevices} active, {EventsToday} events today",
            totalDevices, activeDevices, eventsToday);

        return Ok(stats);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeleteDevice(string id)
    {
        var device = await _context.Devices.FindAsync(id);

        if (device == null)
        {
            return NotFound();
        }

        _context.Devices.Remove(device);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
