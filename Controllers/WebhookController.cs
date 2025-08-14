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

                // Extract trading signal data
                var signal = new TradingSignal
                {
                    Status = SignalStatus.Pending,
                    Timestamp = DateTime.UtcNow
                };

                // Try to extract required fields
                if (!TryExtractField(payload.RootElement, "symbol", out var symbol) ||
                    !TryExtractField(payload.RootElement, "action", out var action) ||
                    !TryExtractDecimal(payload.RootElement, "price", out var price))
                {
                    return BadRequest("Required fields missing or invalid: symbol, action, price");
                }

                signal.Symbol = symbol;
                signal.Action = action;
                signal.Price = price;

                // Optional field
                if (payload.RootElement.TryGetProperty("message", out var messageElement) && 
                    messageElement.ValueKind == JsonValueKind.String)
                {
                    signal.Message = messageElement.GetString();
                }

                // Try to extract timestamp if provided
                if (payload.RootElement.TryGetProperty("timestamp", out var timestampElement) &&
                    timestampElement.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(timestampElement.GetString(), out var timestamp))
                {
                    signal.Timestamp = timestamp;
                }

                // Save signal to database
                await _context.TradingSignals.AddAsync(signal);
                
                // Handle ActiveTradingSignal (unique by Symbol+Type)
                string uniqueKey = $"{signal.Symbol}_{path}"; // Combine Symbol and webhook path as unique key
                
                // Check if an active signal already exists for this Symbol+Type combination
                var existingActiveSignal = await _context.ActiveTradingSignals
                    .FirstOrDefaultAsync(a => a.UniqueKey == uniqueKey);
                
                if (existingActiveSignal != null)
                {
                    // Update existing record
                    existingActiveSignal.Action = signal.Action;
                    existingActiveSignal.Price = signal.Price;
                    existingActiveSignal.Timestamp = signal.Timestamp;
                    existingActiveSignal.Used = false; // Đặt lại trạng thái Used về false khi có tín hiệu mới
                    _logger.LogInformation("Updated existing active signal for {Symbol}/{Type}, reset Used status", signal.Symbol, path);
                }
                else
                {
                    // Create new record
                    var activeSignal = new ActiveTradingSignal
                    {
                        Symbol = signal.Symbol,
                        Action = signal.Action,
                        Price = signal.Price,
                        Timestamp = signal.Timestamp,
                        Type = path,
                        UniqueKey = uniqueKey
                    };
                    
                    await _context.ActiveTradingSignals.AddAsync(activeSignal);
                    _logger.LogInformation("Created new active signal for {Symbol}/{Type}", signal.Symbol, path);
                }
                
                await _context.SaveChangesAsync();

                _logger.LogInformation("Trading signal saved: {Symbol} {Action} at {Price}", 
                    signal.Symbol, signal.Action, signal.Price);

                return Ok(new { message = "Trading signal received", signalId = signal.Id });
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
