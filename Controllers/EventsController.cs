using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IoTDetectorApi.Data;
using IoTDetectorApi.Models;

namespace IoTDetectorApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EventsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EventsController> _logger;

    public EventsController(ApplicationDbContext context, ILogger<EventsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("recent")]
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "limit" })]
    public async Task<ActionResult<IEnumerable<EventLog>>> GetRecentEvents([FromQuery] int limit = 50)
    {
        // Validate and cap limit
        if (limit < 1) limit = 10;
        if (limit > 500) limit = 500;

        _logger.LogInformation("Fetching {Limit} most recent events", limit);

        // Uses descending index IX_EventLogs_Timestamp
        var events = await _context.EventLogs
            .AsNoTracking()
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} recent events", events.Count);

        return Ok(events);
    }

    [HttpGet]
    [ResponseCache(Duration = 30, VaryByQueryKeys = new[] { "level", "skip", "take" })]
    public async Task<ActionResult<IEnumerable<EventLog>>> GetEvents(
        [FromQuery] string? level = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100)
    {
        // Validate and cap pagination
        if (skip < 0) skip = 0;
        if (take < 1) take = 100;
        if (take > 500) take = 500;

        _logger.LogInformation("Fetching events - level: {Level}, skip: {Skip}, take: {Take}",
            level ?? "all", skip, take);

        var query = _context.EventLogs.AsNoTracking();

        if (!string.IsNullOrEmpty(level))
        {
            // Uses index: IX_EventLogs_Level
            query = query.Where(e => e.Level == level);
        }

        // Uses descending index IX_EventLogs_Timestamp
        var events = await query
            .OrderByDescending(e => e.Timestamp)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} events", events.Count);

        return Ok(events);
    }
}
