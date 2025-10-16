using System.Collections.Generic;
using System.Threading.Tasks;
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
    [Route("api/[controller]")]
    public class ActiveSignalsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ActiveSignalsController> _logger;
        private readonly IConfiguration _configuration;

        public ActiveSignalsController(AppDbContext context, ILogger<ActiveSignalsController> logger, IConfiguration configuration)
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
    }
}
