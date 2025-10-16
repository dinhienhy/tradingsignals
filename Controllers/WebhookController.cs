using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingSignalsApi.Data;
using TradingSignalsApi.Models;
using TradingSignalsApi.Services;

namespace TradingSignalsApi.Controllers
{
    [ApiController]
    [Route("webhook")]
    public class WebhookController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WebhookController> _logger;
        private readonly IActiveSignalProcessor _signalProcessor;

        public WebhookController(
            AppDbContext context, 
            ILogger<WebhookController> logger,
            IActiveSignalProcessor signalProcessor)
        {
            _context = context;
            _logger = logger;
            _signalProcessor = signalProcessor;
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

                // Try to extract swing price if provided (optional field)
                decimal? swingPrice = null;
                if (TryExtractDecimal(payload.RootElement, "swing", out var swing))
                {
                    swingPrice = swing;
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
                    existingActiveSignal.Timestamp = signal.Timestamp;
                    existingActiveSignal.Swing = swingPrice?.ToString();
                    existingActiveSignal.Used = false; // Đặt lại trạng thái Used về false khi có tín hiệu mới
                    existingActiveSignal.Resolved = 0; // Đặt lại trạng thái Resolved về 0 khi có tín hiệu mới
                    
                    activeSignal = existingActiveSignal;
                    _logger.LogInformation("Updated existing active signal for {Symbol}/{Type}", signal.Symbol, path);
                }
                else
                {
                    // Create new record
                    activeSignal = new ActiveTradingSignal
                    {
                        Symbol = signal.Symbol,
                        Action = signal.Action,
                        Price = signal.Price,
                        Timestamp = signal.Timestamp,
                        Type = path,
                        UniqueKey = uniqueKey,
                        Swing = swingPrice?.ToString()
                    };
                    
                    await _context.ActiveTradingSignals.AddAsync(activeSignal);
                    _logger.LogInformation("Created new active signal for {Symbol}/{Type}", signal.Symbol, path);
                }
                
                // Save to database first
                await _context.SaveChangesAsync();
                
                // **POST-PROCESS SIGNAL THROUGH BUSINESS RULES**
                // Rules will update fields like Resolved, Swing, etc.
                _logger.LogInformation("Running business rules for {Symbol}/{Type}", signal.Symbol, path);
                var processingResult = await _signalProcessor.ProcessSignalAsync(activeSignal);
                
                // Save any updates made by rules
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Signal processing completed for {Symbol}/{Type}: {Message}", 
                    signal.Symbol, path, processingResult.Message);

                return Ok(new 
                { 
                    message = "Trading signal received and processed", 
                    signalId = signal.Id,
                    processingSummary = processingResult.Message,
                    rulesExecuted = processingResult.ValidationResult?.RuleResults.Count ?? 0
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
