using System;
using System.Text.Json;
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
    public class WebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WebhookController> _logger;

        public WebhookController(AppDbContext context, ILogger<WebhookController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Processes incoming webhooks with dynamic path
        /// </summary>
        /// <param name="path">The dynamic path that identifies the webhook config</param>
        /// <returns>Appropriate HTTP response</returns>
        [HttpPost("{*path}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ProcessWebhook(string path)
        {
            // Log incoming webhook
            _logger.LogInformation("Received webhook request for path: {Path}", path);

            // Find webhook configuration matching the path
            var webhookConfig = await _context.WebhookConfigs
                .FirstOrDefaultAsync(w => w.Path == path);

            if (webhookConfig == null)
            {
                _logger.LogWarning("No webhook configuration found for path: {Path}", path);
                return NotFound("Webhook configuration not found");
            }

            // Read and parse the request body
            try
            {
                using var streamReader = new System.IO.StreamReader(Request.Body);
                var requestBody = await streamReader.ReadToEndAsync();
                var payload = JsonDocument.Parse(requestBody);

                // Verify the secret
                if (!payload.RootElement.TryGetProperty("secret", out var secretElement) || 
                    secretElement.GetString() != webhookConfig.Secret)
                {
                    _logger.LogWarning("Invalid secret provided for webhook path: {Path}", path);
                    return Unauthorized("Invalid secret");
                }

                // Try to extract required fields
                if (!TryExtractField(payload.RootElement, "symbol", out var symbol) ||
                    !TryExtractField(payload.RootElement, "action", out var action) ||
                    !TryExtractDecimal(payload.RootElement, "price", out var price))
                {
                    _logger.LogWarning("Missing required fields in webhook payload for path: {Path}", path);
                    return BadRequest("Required fields missing or invalid: symbol, action, price");
                }

                // Extract timestamp - use provided timestamp or current UTC time
                DateTime signalTimestamp = DateTime.UtcNow;
                if (payload.RootElement.TryGetProperty("timestamp", out var timestampElement))
                {
                    // Try multiple formats
                    if (timestampElement.ValueKind == JsonValueKind.String)
                    {
                        var timestampStr = timestampElement.GetString();
                        if (DateTime.TryParse(timestampStr, out var parsedTimestamp))
                        {
                            signalTimestamp = parsedTimestamp.ToUniversalTime();
                            _logger.LogInformation("Using provided timestamp: {Timestamp}", signalTimestamp);
                        }
                        else if (long.TryParse(timestampStr, out var unixTimestamp))
                        {
                            signalTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixTimestamp).UtcDateTime;
                            _logger.LogInformation("Parsed Unix timestamp: {Timestamp}", signalTimestamp);
                        }
                    }
                    else if (timestampElement.ValueKind == JsonValueKind.Number && timestampElement.TryGetInt64(out var unixTs))
                    {
                        signalTimestamp = DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime;
                        _logger.LogInformation("Parsed Unix timestamp: {Timestamp}", signalTimestamp);
                    }
                }
                else
                {
                    _logger.LogInformation("No timestamp provided, using current UTC time: {Timestamp}", signalTimestamp);
                }

                // Extract swing price if provided
                decimal? swingPrice = null;
                if (TryExtractDecimal(payload.RootElement, "swing", out var swing))
                {
                    swingPrice = swing;
                    _logger.LogInformation("Swing price extracted: {Swing} for {Symbol}", swing, symbol);
                }
                else
                {
                    _logger.LogDebug("No swing price provided for {Symbol}", symbol);
                }

                // Extract trading signal data
                var signal = new TradingSignal
                {
                    Symbol = symbol,
                    Action = action,
                    Price = price,
                    Status = SignalStatus.Pending,
                    Timestamp = signalTimestamp
                };

                // Optional message field
                if (payload.RootElement.TryGetProperty("message", out var messageElement) && 
                    messageElement.ValueKind == JsonValueKind.String)
                {
                    signal.Message = messageElement.GetString();
                }

                // Save trading signal to history first
                await _context.TradingSignals.AddAsync(signal);
                
                // Handle ActiveTradingSignal (unique by Symbol+Type)
                string uniqueKey = $"{signal.Symbol}_{path}"; // Combine Symbol and webhook path as unique key
                
                // Check if an active signal already exists for this Symbol+Type combination
                var existingActiveSignal = await _context.ActiveTradingSignals
                    .FirstOrDefaultAsync(a => a.UniqueKey == uniqueKey);
                
                ActiveTradingSignal activeSignal;
                
                if (existingActiveSignal != null)
                {
                    // Update existing record
                    existingActiveSignal.Action = signal.Action;
                    existingActiveSignal.Price = signal.Price;
                    existingActiveSignal.Timestamp = signalTimestamp;
                    existingActiveSignal.Swing = swingPrice;
                    existingActiveSignal.Used = false; // Reset Used to false for new signal
                    existingActiveSignal.Resolved = false; // Reset Resolved to false for new signal
                    
                    activeSignal = existingActiveSignal;
                    _logger.LogInformation("Updated active signal: {Symbol}/{Type} - Action={Action}, Price={Price}, Swing={Swing}, Timestamp={Timestamp}", 
                        signal.Symbol, path, signal.Action, signal.Price, swingPrice, signalTimestamp);
                }
                else
                {
                    // Create new record
                    activeSignal = new ActiveTradingSignal
                    {
                        Symbol = signal.Symbol,
                        Action = signal.Action,
                        Price = signal.Price,
                        Timestamp = signalTimestamp,
                        Type = path,
                        UniqueKey = uniqueKey,
                        Swing = swingPrice
                    };
                    
                    await _context.ActiveTradingSignals.AddAsync(activeSignal);
                    _logger.LogInformation("Created new active signal: {Symbol}/{Type} - Action={Action}, Price={Price}, Swing={Swing}, Timestamp={Timestamp}", 
                        signal.Symbol, path, signal.Action, signal.Price, swingPrice, signalTimestamp);
                }
                
                // Save to database
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Trading signal saved: {Symbol} {Action} at {Price} for type {Type}", 
                    signal.Symbol, signal.Action, signal.Price, path);

                return Ok(new 
                { 
                    message = "Trading signal received", 
                    signalId = signal.Id,
                    activeSignalId = activeSignal.Id,
                    symbol = signal.Symbol,
                    action = signal.Action,
                    price = signal.Price,
                    swing = swingPrice,
                    timestamp = signalTimestamp,
                    type = path
                });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON payload received");
                return BadRequest("Invalid JSON payload");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing webhook");
                return StatusCode(500, "An error occurred while processing the webhook");
            }
        }

        /// <summary>
        /// Helper method to extract a string field from JSON
        /// </summary>
        private bool TryExtractField(JsonElement element, string fieldName, out string value)
        {
            value = string.Empty;
            
            if (element.TryGetProperty(fieldName, out var jsonElement) && 
                jsonElement.ValueKind == JsonValueKind.String)
            {
                value = jsonElement.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(value);
            }
            
            return false;
        }

        /// <summary>
        /// Helper method to extract a decimal field from JSON
        /// </summary>
        private bool TryExtractDecimal(JsonElement element, string fieldName, out decimal value)
        {
            value = 0;
            
            if (element.TryGetProperty(fieldName, out var jsonElement))
            {
                if (jsonElement.ValueKind == JsonValueKind.Number)
                {
                    return jsonElement.TryGetDecimal(out value);
                }
                else if (jsonElement.ValueKind == JsonValueKind.String)
                {
                    return decimal.TryParse(jsonElement.GetString(), out value);
                }
            }
            
            return false;
        }
    }
}
