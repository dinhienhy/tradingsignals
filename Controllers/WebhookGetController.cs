using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingSignalsApi.Data;
using TradingSignalsApi.Models;

namespace TradingSignalsApi.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookGetController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WebhookGetController> _logger;

        public WebhookGetController(AppDbContext context, ILogger<WebhookGetController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get active trading signal for a specific webhook path
        /// </summary>
        /// <param name="path">The webhook path to get active signal for</param>
        /// <returns>The active trading signal for this path or NotFound</returns>
        [HttpGet("{*path}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ActiveTradingSignal>> GetActiveSignalForPath(string path)
        {
            _logger.LogInformation("Received GET request for webhook path: {Path}", path);

            // Find active trading signals matching the path (Type)
            var activeSignals = await _context.ActiveTradingSignals
                .Where(s => s.Type == path)
                .ToListAsync();

            if (activeSignals.Count == 0)
            {
                _logger.LogWarning("No active signals found for path: {Path}", path);
                return NotFound("No active signals found for this webhook path");
            }

            _logger.LogInformation("Found {Count} active signals for path: {Path}", activeSignals.Count, path);
            return Ok(activeSignals);
        }

        /// <summary>
        /// Get simplified active trading signal for a specific webhook path and symbol
        /// </summary>
        /// <param name="path">The webhook path to get active signal for</param>
        /// <param name="symbol">The symbol to get active signal for</param>
        /// <returns>Only the Action and Price of the active signal or NotFound</returns>
        [HttpGet("{path}/symbol/{symbol}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> GetSimplifiedActiveSignal(string path, string symbol)
        {
            _logger.LogInformation("Received simplified GET request for path: {Path} and symbol: {Symbol}", path, symbol);
            
            // Generate the unique key as it's done in WebhookController
            var uniqueKey = $"{symbol}_{path}";
            
            // Find the exact active signal with this unique key
            var activeSignal = await _context.ActiveTradingSignals
                .FirstOrDefaultAsync(s => s.UniqueKey == uniqueKey);
            
            if (activeSignal == null)
            {
                _logger.LogWarning("No active signal found for path: {Path} and symbol: {Symbol}", path, symbol);
                return NotFound("No active signal found for this webhook path and symbol combination");
            }
            
            // Return only the Action and Price
            var result = new
            {
                Action = activeSignal.Action,
                Price = activeSignal.Price
            };
            
            _logger.LogInformation("Found simplified active signal for path: {Path} and symbol: {Symbol}", path, symbol);
            return Ok(result);
        }
    }
}
