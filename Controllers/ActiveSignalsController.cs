using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingSignalsApi.Data;
using TradingSignalsApi.Models;

namespace TradingSignalsApi.Controllers
{
    [ApiController]
    [Route("api/active-signals")]
    public class ActiveSignalsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ActiveSignalsController> _logger;
        private readonly IConfiguration _configuration;

        public ActiveSignalsController(
            AppDbContext context, 
            ILogger<ActiveSignalsController> logger, 
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        private string GetApiKey()
        {
            var authHeader = HttpContext.Request.Headers["X-API-Key"].ToString();
            return string.IsNullOrEmpty(authHeader) ? string.Empty : authHeader;
        }

        /// <summary>
        /// Get all active trading signals (one per Symbol+Type combination)
        /// </summary>
        /// <returns>List of active trading signals</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> GetActiveSignals()
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? System.Environment.GetEnvironmentVariable("API_KEY");
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to active signals");
                return Unauthorized("Invalid API key");
            }

            var activeSignals = await _context.ActiveTradingSignals
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();

            if (activeSignals.Count == 0)
            {
                // Trả về mảng rỗng thay vì NoContent để tránh lỗi JSON parse ở client
                return Ok(new List<object>());
            }

            // Map to DTO to ensure all fields are included
            var result = activeSignals.Select(s => new
            {
                id = s.Id,
                symbol = s.Symbol,
                action = s.Action,
                price = s.Price,
                timestamp = DateTime.SpecifyKind(s.Timestamp, DateTimeKind.Utc),
                type = s.Type,
                uniqueKey = s.UniqueKey,
                used = s.Used,
                resolved = s.Resolved,
                swing = s.Swing  // Explicitly include Swing
            }).ToList();

            _logger.LogInformation("Retrieved {Count} active signals", activeSignals.Count);
            return Ok(result);
        }

        /// <summary>
        /// Get active trading signals by type (webhook path)
        /// </summary>
        /// <param name="type">The webhook path/type to filter by</param>
        /// <returns>List of active trading signals for the specified type</returns>
        [HttpGet("bytype/{type}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> GetActiveSignalsByType(string type)
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? System.Environment.GetEnvironmentVariable("API_KEY");
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to active signals by type");
                return Unauthorized("Invalid API key");
            }

            var activeSignals = await _context.ActiveTradingSignals
                .Where(s => s.Type == type)
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();

            if (activeSignals.Count == 0)
            {
                // Trả về mảng rỗng thay vì NoContent để tránh lỗi JSON parse ở client
                return Ok(new List<object>());
            }

            // Map to DTO to ensure all fields are included
            var result = activeSignals.Select(s => new
            {
                id = s.Id,
                symbol = s.Symbol,
                action = s.Action,
                price = s.Price,
                timestamp = DateTime.SpecifyKind(s.Timestamp, DateTimeKind.Utc),
                type = s.Type,
                uniqueKey = s.UniqueKey,
                used = s.Used,
                resolved = s.Resolved,
                swing = s.Swing
            }).ToList();

            _logger.LogInformation("Retrieved {Count} active signals for type {Type}", activeSignals.Count, type);
            return Ok(result);
        }

        /// <summary>
        /// Mark a signal as used by MT5 bot
        /// </summary>
        /// <param name="id">The signal ID to mark as used</param>
        /// <returns>The updated signal</returns>
        [HttpPut("markused/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ActiveTradingSignal>> MarkAsUsed(int id)
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? System.Environment.GetEnvironmentVariable("API_KEY");
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to mark signal as used");
                return Unauthorized("Invalid API key");
            }

            var signal = await _context.ActiveTradingSignals.FindAsync(id);
            
            if (signal == null)
            {
                _logger.LogWarning("Signal with ID {Id} not found for marking as used", id);
                return NotFound($"Signal with ID {id} not found");
            }

            // Đánh dấu tín hiệu là đã được sử dụng
            signal.Used = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Signal with ID {Id} marked as used", id);
            return Ok(signal);
        }
        
        /// <summary>
        /// Get unused active trading signals (one per Symbol+Type combination)
        /// </summary>
        /// <returns>List of unused active trading signals</returns>
        [HttpGet("unused")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> GetUnusedActiveSignals()
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? System.Environment.GetEnvironmentVariable("API_KEY");
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to unused active signals");
                return Unauthorized("Invalid API key");
            }

            var activeSignals = await _context.ActiveTradingSignals
                .Where(s => s.Used == false)
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();

            // Map to DTO to ensure all fields are included
            var result = activeSignals.Select(s => new
            {
                id = s.Id,
                symbol = s.Symbol,
                action = s.Action,
                price = s.Price,
                timestamp = DateTime.SpecifyKind(s.Timestamp, DateTimeKind.Utc),
                type = s.Type,
                uniqueKey = s.UniqueKey,
                used = s.Used,
                resolved = s.Resolved,
                swing = s.Swing
            }).ToList();

            _logger.LogInformation("Retrieved {Count} unused active signals", activeSignals.Count);
            return Ok(result);
        }

        /// <summary>
        /// Mark an active signal as resolved
        /// </summary>
        [HttpPut("resolve/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> ResolveSignal(int id)
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? Environment.GetEnvironmentVariable("API_KEY");
            
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to resolve signal {Id}", id);
                return Unauthorized("Invalid API key");
            }

            var signal = await _context.ActiveTradingSignals.FindAsync(id);
            
            if (signal == null)
            {
                _logger.LogWarning("Signal {Id} not found", id);
                return NotFound($"Signal with ID {id} not found");
            }

            signal.Resolved = true;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Signal {Id} marked as resolved", id);
            
            return Ok(new { 
                id = signal.Id, 
                resolved = signal.Resolved,
                message = "Signal marked as resolved successfully"
            });
        }

        /// <summary>
        /// Update swing value for an active signal
        /// </summary>
        [HttpPut("swing/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> UpdateSwing(int id, [FromBody] SwingUpdateRequest request)
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? Environment.GetEnvironmentVariable("API_KEY");
            
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to update swing for signal {Id}", id);
                return Unauthorized("Invalid API key");
            }

            if (request == null || request.Swing <= 0)
            {
                return BadRequest("Invalid swing value");
            }

            var signal = await _context.ActiveTradingSignals.FindAsync(id);
            
            if (signal == null)
            {
                _logger.LogWarning("Signal {Id} not found", id);
                return NotFound($"Signal with ID {id} not found");
            }

            signal.Swing = request.Swing;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Signal {Id} swing updated to {Swing}", id, request.Swing);
            
            return Ok(new { 
                id = signal.Id, 
                swing = signal.Swing,
                message = "Swing value updated successfully"
            });
        }

        /// <summary>
        /// Delete an active signal
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> DeleteSignal(int id)
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? Environment.GetEnvironmentVariable("API_KEY");
            
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to delete signal {Id}", id);
                return Unauthorized("Invalid API key");
            }

            var signal = await _context.ActiveTradingSignals.FindAsync(id);
            
            if (signal == null)
            {
                _logger.LogWarning("Signal {Id} not found", id);
                return NotFound($"Signal with ID {id} not found");
            }

            _context.ActiveTradingSignals.Remove(signal);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Signal {Id} deleted successfully", id);
            
            return Ok(new { 
                id = id,
                message = "Signal deleted successfully"
            });
        }
    }

    /// <summary>
    /// Request model for updating swing value
    /// </summary>
    public class SwingUpdateRequest
    {
        public decimal Swing { get; set; }
    }
}
