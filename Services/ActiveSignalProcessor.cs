using Microsoft.EntityFrameworkCore;
using TradingSignalsApi.Data;
using TradingSignalsApi.Models;
using TradingSignalsApi.BusinessRules;

namespace TradingSignalsApi.Services;

/// <summary>
/// Service that processes active trading signals through business rules
/// </summary>
public class ActiveSignalProcessor : IActiveSignalProcessor
{
    private readonly AppDbContext _context;
    private readonly SignalRuleEngine _ruleEngine;
    private readonly ILogger<ActiveSignalProcessor> _logger;
    
    public ActiveSignalProcessor(
        AppDbContext context, 
        SignalRuleEngine ruleEngine,
        ILogger<ActiveSignalProcessor> logger)
    {
        _context = context;
        _ruleEngine = ruleEngine;
        _logger = logger;
    }
    
    public async Task<ProcessingResult> ProcessSignalAsync(ActiveTradingSignal signal)
    {
        try
        {
            _logger.LogInformation("Processing signal: {Symbol} {Action} at {Price}", 
                signal.Symbol, signal.Action, signal.Price);
            
            // Get existing signals for context
            var existingSignals = await _context.ActiveTradingSignals
                .OrderByDescending(s => s.Timestamp)
                .Take(100) // Limit for performance
                .ToListAsync();
            
            var context = new SignalContext
            {
                ExistingSignals = existingSignals,
                ProcessingTime = DateTime.UtcNow
            };
            
            // Validate through rule engine
            var validationResult = await _ruleEngine.ValidateAsync(signal, context);
            
            if (!validationResult.IsValid)
            {
                _logger.LogWarning("Signal validation failed: {Summary}", validationResult.GetSummary());
                
                return new ProcessingResult
                {
                    Success = false,
                    Message = validationResult.GetSummary(),
                    ValidationResult = validationResult
                };
            }
            
            _logger.LogInformation("Signal validation passed: {Symbol} {Action}", signal.Symbol, signal.Action);
            
            return new ProcessingResult
            {
                Success = true,
                Message = "Signal processed successfully",
                ValidationResult = validationResult,
                ProcessedSignal = signal
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing signal: {Symbol} {Action}", signal.Symbol, signal.Action);
            
            return new ProcessingResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
    
    public async Task RunMaintenanceAsync()
    {
        try
        {
            _logger.LogInformation("Running signal maintenance tasks...");
            
            // Get all active signals
            var signals = await _context.ActiveTradingSignals
                .Where(s => s.Resolved == 0)
                .ToListAsync();
            
            var context = new SignalContext
            {
                ExistingSignals = signals,
                ProcessingTime = DateTime.UtcNow
            };
            
            int expiredCount = 0;
            
            // Run expiration rule on each signal
            foreach (var signal in signals)
            {
                var validationResult = await _ruleEngine.ValidateAsync(signal, context);
                
                if (signal.Resolved == 1) // If rule marked as resolved
                {
                    expiredCount++;
                }
            }
            
            if (expiredCount > 0)
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Expired {Count} signals", expiredCount);
            }
            
            _logger.LogInformation("Maintenance completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running maintenance");
        }
    }
}
