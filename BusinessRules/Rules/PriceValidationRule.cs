using TradingSignalsApi.Models;

namespace TradingSignalsApi.BusinessRules.Rules;

/// <summary>
/// Validates that the price is within acceptable ranges
/// </summary>
public class PriceValidationRule : ISignalRule
{
    private readonly ILogger<PriceValidationRule> _logger;
    
    public string RuleName => "PriceValidationRule";
    public int Priority => 5; // High priority - validate early
    
    public PriceValidationRule(ILogger<PriceValidationRule> logger)
    {
        _logger = logger;
    }
    
    public Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        // Basic validation
        if (signal.Price <= 0)
        {
            _logger.LogError("Invalid price: {Price} for {Symbol}", signal.Price, signal.Symbol);
            return Task.FromResult(RuleResult.Failure("Price must be greater than 0", shouldContinue: false));
        }
        
        // Check for unrealistic price spikes
        var recentSignals = context.ExistingSignals
            .Where(s => s.Symbol == signal.Symbol && s.Id != signal.Id)
            .OrderByDescending(s => s.Timestamp)
            .Take(10)
            .ToList();
        
        if (recentSignals.Any())
        {
            var avgPrice = recentSignals.Average(s => s.Price);
            var priceChange = Math.Abs((signal.Price - avgPrice) / avgPrice);
            
            // If price changed more than 5% from recent average, flag as suspicious
            if (priceChange > 0.05)
            {
                _logger.LogWarning("Suspicious price spike detected: {Price} vs avg {AvgPrice} for {Symbol}", 
                    signal.Price, avgPrice, signal.Symbol);
                
                return Task.FromResult(RuleResult.Failure(
                    $"Price spike detected: {priceChange:P2} change from average",
                    shouldContinue: true)); // Continue to other rules for review
            }
        }
        
        return Task.FromResult(RuleResult.Success());
    }
}
