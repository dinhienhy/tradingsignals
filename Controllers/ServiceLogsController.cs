using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingSignalsApi.Data;
using TradingSignalsApi.Models;

namespace TradingSignalsApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ServiceLogsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ServiceLogsController> _logger;

    public ServiceLogsController(AppDbContext context, ILogger<ServiceLogsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Get recent service logs
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ServiceLog>>> GetLogs(
        [FromQuery] int limit = 100,
        [FromQuery] string? level = null,
        [FromQuery] string? source = null,
        [FromQuery] string? symbol = null)
    {
        var query = _context.ServiceLogs.AsQueryable();

        if (!string.IsNullOrEmpty(level))
        {
            query = query.Where(l => l.Level == level);
        }

        if (!string.IsNullOrEmpty(source))
        {
            query = query.Where(l => l.Source == source);
        }

        if (!string.IsNullOrEmpty(symbol))
        {
            query = query.Where(l => l.Symbol == symbol);
        }

        var logs = await query
            .OrderByDescending(l => l.Timestamp)
            .Take(limit)
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Get log statistics
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetStats()
    {
        var now = DateTime.UtcNow;
        var last24h = now.AddHours(-24);
        var lastHour = now.AddHours(-1);

        var stats = new
        {
            TotalLogs = await _context.ServiceLogs.CountAsync(),
            Last24Hours = await _context.ServiceLogs.CountAsync(l => l.Timestamp > last24h),
            LastHour = await _context.ServiceLogs.CountAsync(l => l.Timestamp > lastHour),
            ByLevel = await _context.ServiceLogs
                .Where(l => l.Timestamp > last24h)
                .GroupBy(l => l.Level)
                .Select(g => new { Level = g.Key, Count = g.Count() })
                .ToListAsync(),
            BySource = await _context.ServiceLogs
                .Where(l => l.Timestamp > last24h)
                .GroupBy(l => l.Source)
                .Select(g => new { Source = g.Key, Count = g.Count() })
                .ToListAsync(),
            RecentErrors = await _context.ServiceLogs
                .Where(l => l.Level == "ERROR" && l.Timestamp > last24h)
                .OrderByDescending(l => l.Timestamp)
                .Take(10)
                .Select(l => new { l.Timestamp, l.Source, l.Message })
                .ToListAsync()
        };

        return Ok(stats);
    }

    /// <summary>
    /// Clear old logs (older than specified days)
    /// </summary>
    [HttpDelete("cleanup")]
    public async Task<ActionResult<object>> CleanupOldLogs([FromQuery] int daysToKeep = 7)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);
        var oldLogs = await _context.ServiceLogs
            .Where(l => l.Timestamp < cutoffDate)
            .ToListAsync();

        var count = oldLogs.Count;
        _context.ServiceLogs.RemoveRange(oldLogs);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} logs older than {Days} days", count, daysToKeep);

        return Ok(new { message = $"Cleaned up {count} old logs", deletedCount = count });
    }
}
