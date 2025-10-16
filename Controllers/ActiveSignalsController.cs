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
using TradingSignalsApi.Services;

namespace TradingSignalsApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActiveSignalsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ActiveSignalsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IActiveSignalProcessor _signalProcessor;

        public ActiveSignalsController(
            AppDbContext context, 
            ILogger<ActiveSignalsController> logger, 
            IConfiguration configuration,
            IActiveSignalProcessor signalProcessor)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _signalProcessor = signalProcessor;
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
        public async Task<ActionResult<IEnumerable<ActiveTradingSignal>>> GetActiveSignals()
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
                return Ok(new List<ActiveTradingSignal>());
            }

            _logger.LogInformation("Retrieved {Count} active signals", activeSignals.Count);
            return Ok(activeSignals);
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
        public async Task<ActionResult<IEnumerable<ActiveTradingSignal>>> GetActiveSignalsByType(string type)
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
                return Ok(new List<ActiveTradingSignal>());
            }

            _logger.LogInformation("Retrieved {Count} active signals for type {Type}", activeSignals.Count, type);
            return Ok(activeSignals);
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
        public async Task<ActionResult<IEnumerable<ActiveTradingSignal>>> GetUnusedActiveSignals()
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

            _logger.LogInformation("Retrieved {Count} unused active signals", activeSignals.Count);
            return Ok(activeSignals);
        }

        /// <summary>
        /// Validate a signal through business rules (without saving)
        /// </summary>
        /// <param name="signal">The signal to validate</param>
        /// <returns>Validation result</returns>
        [HttpPost("validate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<object>> ValidateSignal([FromBody] ActiveTradingSignal signal)
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? System.Environment.GetEnvironmentVariable("API_KEY");
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to validate signal");
                return Unauthorized("Invalid API key");
            }

            if (signal == null)
            {
                return BadRequest("Signal data is required");
            }

            var result = await _signalProcessor.ProcessSignalAsync(signal);

            return Ok(new
            {
                success = result.Success,
                message = result.Message,
                validationDetails = result.ValidationResult,
                processedSignal = result.ProcessedSignal
            });
        }

        /// <summary>
        /// Run maintenance tasks (expire old signals, etc.)
        /// </summary>
        /// <returns>Status message</returns>
        [HttpPost("maintenance")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<object>> RunMaintenance()
        {
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? System.Environment.GetEnvironmentVariable("API_KEY");
            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to run maintenance");
                return Unauthorized("Invalid API key");
            }

            await _signalProcessor.RunMaintenanceAsync();

            return Ok(new { message = "Maintenance completed successfully" });
        }
    }
}
