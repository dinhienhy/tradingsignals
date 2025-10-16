using TradingSignalsApi.Models;

namespace TradingSignalsApi.BusinessRules.Rules;

/// <summary>
/// Business rule for EntryCHoCH (Change of Character) signals
/// CHoCH indicates a potential trend reversal
/// </summary>
public class EntryCHoCHRule : ISignalRule
{
    private readonly ILogger<EntryCHoCHRule> _logger;
    
    public string RuleName => "EntryCHoCHRule";
    public int Priority => 25; // Run after basic validation but before expiration
    
    public EntryCHoCHRule(ILogger<EntryCHoCHRule> logger)
    {
        _logger = logger;
    }
    
    public Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        // Only process signals from EntryCHoCH webhook
        if (signal.Type?.ToLower() != "entrychoch")
        {
            return Task.FromResult(RuleResult.Success("Not an EntryCHoCH signal"));
        }
        
        _logger.LogInformation("Processing EntryCHoCH signal: {Symbol} {Action} at {Price}", 
            signal.Symbol, signal.Action, signal.Price);
        
        // Get recent signals for the same symbol
        var recentSignals = context.ExistingSignals
            .Where(s => s.Symbol == signal.Symbol 
                     && s.Type?.ToLower() == "entrychoch"
                     && s.Id != signal.Id)
            .OrderByDescending(s => s.Timestamp)
            .Take(5)
            .ToList();
        
        // Rule 1: Check for opposite action within recent timeframe (potential reversal confirmation)
        var oppositeAction = signal.Action == "BUY" ? "SELL" : "BUY";
        var recentOpposite = recentSignals
            .Where(s => s.Action == oppositeAction)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefault();
        
        if (recentOpposite != null)
        {
            var timeDiff = signal.Timestamp - recentOpposite.Timestamp;
            
            // If opposite signal was within 1 hour, this confirms trend change
            if (timeDiff.TotalHours < 1)
            {
                _logger.LogInformation("CHoCH confirmed: Found opposite {OppositeAction} signal {Minutes} minutes ago", 
                    oppositeAction, timeDiff.TotalMinutes);
                
                // Mark the opposite signal as resolved since trend changed
                recentOpposite.Resolved = 1;
                
                // Set swing level from the reversal point
                if (!string.IsNullOrEmpty(recentOpposite.Swing))
                {
                    signal.Swing = recentOpposite.Swing;
                    _logger.LogInformation("Swing level inherited from reversal: {Swing}", signal.Swing);
                }
            }
        }
        
        // Rule 2: Check for too frequent CHoCH signals (may indicate choppy market)
        var recentChochCount = recentSignals
            .Where(s => s.Timestamp > context.ProcessingTime.AddHours(-2))
            .Count();
        
        if (recentChochCount >= 3)
        {
            _logger.LogWarning("Too many CHoCH signals ({Count}) in 2 hours for {Symbol} - Choppy market detected", 
                recentChochCount + 1, signal.Symbol);
            
            // Don't reject, just log warning
            // Market is choppy but signal is still valid
        }
        
        // Rule 3: Validate price movement makes sense for CHoCH
        if (recentSignals.Any())
        {
            var lastSignal = recentSignals.First();
            var priceChange = Math.Abs(signal.Price - lastSignal.Price);
            var priceChangePercent = (priceChange / lastSignal.Price) * 100;
            
            // CHoCH should have reasonable price movement (not too small, not too large)
            if (priceChangePercent < 0.05) // Less than 0.05% = 5 pips for Forex
            {
                _logger.LogWarning("CHoCH price movement too small: {Percent}% - may not be significant", priceChangePercent);
                // Don't reject, just log - could still be valid in ranging market
            }
            
            if (priceChangePercent > 2.0) // More than 2% is unusual for short-term CHoCH
            {
                _logger.LogWarning("CHoCH price movement too large: {Percent}% - May be news event", priceChangePercent);
                // Don't fail, just log warning - could be legitimate
            }
        }
        
        // Rule 4: Auto-set resolved for old CHoCH if not yet resolved
        foreach (var oldSignal in recentSignals.Where(s => s.Resolved == 0))
        {
            var age = context.ProcessingTime - oldSignal.Timestamp;
            if (age.TotalHours > 4) // CHoCH signals are short-term, expire after 4 hours
            {
                oldSignal.Resolved = 1;
                _logger.LogInformation("Auto-resolved old CHoCH signal: {Symbol} {Action}, age: {Hours}h", 
                    oldSignal.Symbol, oldSignal.Action, age.TotalHours);
            }
        }
        
        _logger.LogInformation("EntryCHoCH signal validated successfully: {Symbol} {Action}", 
            signal.Symbol, signal.Action);
        
        return Task.FromResult(RuleResult.Success("CHoCH signal validated"));
    }
}
