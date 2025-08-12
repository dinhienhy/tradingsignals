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
    [Route("config/webhooks")]
    public class WebhooksController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<WebhooksController> _logger;

        public WebhooksController(
            AppDbContext context,
            IConfiguration configuration,
            ILogger<WebhooksController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Get all webhook configurations
        /// </summary>
        /// <returns>List of webhook configurations</returns>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<WebhookConfig>>> GetWebhookConfigs()
        {
            if (!ValidateConfigApiKey())
            {
                return Unauthorized("Invalid API key");
            }

            var configs = await _context.WebhookConfigs.ToListAsync();
            _logger.LogInformation("Retrieved {Count} webhook configurations", configs.Count);
            return Ok(configs);
        }

        /// <summary>
        /// Creates a new webhook configuration
        /// </summary>
        /// <param name="webhookConfig">Webhook configuration to create</param>
        /// <returns>Created webhook configuration</returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<ActionResult<WebhookConfig>> CreateWebhookConfig(WebhookConfig webhookConfig)
        {
            if (!ValidateConfigApiKey())
            {
                return Unauthorized("Invalid API key");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Check if path is already in use
            if (await _context.WebhookConfigs.AnyAsync(w => w.Path == webhookConfig.Path))
            {
                _logger.LogWarning("Attempt to create webhook with duplicate path: {Path}", webhookConfig.Path);
                return Conflict("A webhook with this path already exists");
            }

            _context.WebhookConfigs.Add(webhookConfig);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created new webhook configuration with ID {Id} and path {Path}", 
                webhookConfig.Id, webhookConfig.Path);

            return CreatedAtAction(nameof(GetWebhookConfigs), new { id = webhookConfig.Id }, webhookConfig);
        }

        /// <summary>
        /// Deletes a webhook configuration by ID
        /// </summary>
        /// <param name="id">ID of the webhook configuration to delete</param>
        /// <returns>No content</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteWebhookConfig(int id)
        {
            if (!ValidateConfigApiKey())
            {
                return Unauthorized("Invalid API key");
            }

            var webhookConfig = await _context.WebhookConfigs.FindAsync(id);
            if (webhookConfig == null)
            {
                _logger.LogWarning("Attempt to delete non-existent webhook with ID: {Id}", id);
                return NotFound();
            }

            _context.WebhookConfigs.Remove(webhookConfig);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted webhook configuration with ID {Id} and path {Path}", 
                id, webhookConfig.Path);

            return NoContent();
        }

        /// <summary>
        /// Helper method to validate the configuration API key
        /// </summary>
        private bool ValidateConfigApiKey()
        {
            // Get API key from header
            if (!Request.Headers.TryGetValue("ConfigApiKey", out var apiKey))
            {
                _logger.LogWarning("Missing ConfigApiKey header");
                return false;
            }

            // Get configured API key
            var configuredApiKey = _configuration["CONFIG_API_KEY"] ?? 
                                   Environment.GetEnvironmentVariable("CONFIG_API_KEY");

            if (string.IsNullOrEmpty(configuredApiKey))
            {
                _logger.LogError("CONFIG_API_KEY is not configured");
                return false;
            }

            var isValid = apiKey == configuredApiKey;
            if (!isValid)
            {
                _logger.LogWarning("Invalid ConfigApiKey provided");
            }

            return isValid;
        }
    }
}
