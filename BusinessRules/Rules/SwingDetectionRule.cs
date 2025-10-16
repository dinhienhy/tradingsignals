using TradingSignalsApi.Models;

namespace TradingSignalsApi.BusinessRules.Rules;

/// <summary>
/// Detects swing levels and updates the Swing field
/// </summary>
public class SwingDetectionRule : ISignalRule
{
    private readonly ILogger<SwingDetectionRule> _logger;
    
    public string RuleName => "SwingDetectionRule";
    public int Priority => 20;
    
    public SwingDetectionRule(ILogger<SwingDetectionRule> logger)
    {
        _logger = logger;
    }
    
    public Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        // Skip if swing already set
        if (!string.IsNullOrEmpty(signal.Swing))
        {
            return Task.FromResult(RuleResult.Success("Swing already set"));
        }
        
        // Get recent signals for the same symbol
        var recentSignals = context.ExistingSignals
            .Where(s => s.Symbol == signal.Symbol 
                     && s.Id != signal.Id
                     && !string.IsNullOrEmpty(s.Swing))
            .OrderByDescending(s => s.Timestamp)
            .Take(5)
            .ToList();
        
        if (recentSignals.Any())
        {
            // Logic: Check if current price is near any recent swing
            // This is a simplified example - customize based on your needs
            var nearestSwing = recentSignals
                .Select(s => new { Signal = s, Distance = Math.Abs(double.Parse(s.Swing!) - signal.Price) })
                .OrderBy(x => x.Distance)
                .FirstOrDefault();
            
            if (nearestSwing != null && nearestSwing.Distance < 0.001) // 10 pips tolerance
            {
                signal.Swing = nearestSwing.Signal.Swing;
                _logger.LogInformation("Swing level detected: {Swing} for {Symbol}", signal.Swing, signal.Symbol);
                return Task.FromResult(RuleResult.Success($"Swing detected: {signal.Swing}"));
            }
        }
        
        // If no swing detected, set current price as potential swing
        signal.Swing = signal.Price.ToString("F5");
        _logger.LogInformation("New swing level set: {Swing} for {Symbol}", signal.Swing, signal.Symbol);
        
        return Task.FromResult(RuleResult.Success($"New swing: {signal.Swing}"));
    }
}
