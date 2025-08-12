using System;
using System.Collections.Generic;
using System.Linq;
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
    [Route("signals")]
    public class SignalsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SignalsController> _logger;

        public SignalsController(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<SignalsController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Gets all trading signals with optional status filter
        /// </summary>
        /// <param name="status">Filter by status: "pending", "processed", or "all" (default)</param>
        /// <returns>List of trading signals</returns>
        [HttpGet]
        [Route("~/api/signals")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<TradingSignal>>> GetSignals([FromQuery] string status = "all")
        {
            // Verify API key
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? Environment.GetEnvironmentVariable("API_KEY");

            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to signals");
                return Unauthorized("Invalid API key");
            }

            // Get signals based on status filter
            IQueryable<TradingSignal> query = _context.TradingSignals;
            
            // Apply status filter if specified
            if (!string.IsNullOrEmpty(status) && status.ToLower() != "all")
            {
                if (status.ToLower() == "pending")
                {
                    query = query.Where(s => s.Status == SignalStatus.Pending);
                }
                else if (status.ToLower() == "processed")
                {
                    query = query.Where(s => s.Status == SignalStatus.Processed);
                }
            }

            // Order by timestamp, newest first
            var signals = await query.OrderByDescending(s => s.Timestamp).ToListAsync();

            _logger.LogInformation("Returning {Count} signals with filter: {Status}", signals.Count, status);

            return Ok(signals);
        }
        
        /// <summary>
        /// Retrieves all pending trading signals and marks them as processed
        /// </summary>
        /// <returns>List of pending trading signals</returns>
        [HttpGet("pending")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<TradingSignal>>> GetPendingSignals()
        {
            // Verify API key
            var apiKey = GetApiKey();
            var configuredApiKey = _configuration["API_KEY"] ?? Environment.GetEnvironmentVariable("API_KEY");

            if (string.IsNullOrEmpty(apiKey) || apiKey != configuredApiKey)
            {
                _logger.LogWarning("Unauthorized access attempt to pending signals");
                return Unauthorized("Invalid API key");
            }

            // Get pending signals
            var pendingSignals = await _context.TradingSignals
                .Where(s => s.Status == SignalStatus.Pending)
                .OrderByDescending(s => s.Timestamp)
                .ToListAsync();

            if (pendingSignals.Count == 0)
            {
                _logger.LogInformation("No pending signals found");
                return NoContent();
            }

            _logger.LogInformation("Returning {Count} pending signals", pendingSignals.Count);

            // Update signals to processed
            foreach (var signal in pendingSignals)
            {
                signal.Status = SignalStatus.Processed;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Marked {Count} signals as processed", pendingSignals.Count);

            return Ok(pendingSignals);
        }

        /// <summary>
        /// Helper method to extract API key from request
        /// </summary>
        private string GetApiKey()
        {
            // Try to get API key from header
            if (Request.Headers.TryGetValue("ApiKey", out var headerValues))
            {
                return headerValues.FirstOrDefault() ?? string.Empty;
            }

            // Try to get API key from query string
            return Request.Query.TryGetValue("apiKey", out var queryValues) 
                ? queryValues.FirstOrDefault() ?? string.Empty 
                : string.Empty;
        }
    }
}
