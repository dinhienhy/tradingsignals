using TradingSignalsApi.Models;

namespace TradingSignalsApi.BusinessRules.Rules;

/// <summary>
/// Business rule for EntryBOS (Break of Structure) signals
/// BOS indicates trend continuation after breaking a key level
/// </summary>
public class EntryBOSRule : ISignalRule
{
    private readonly ILogger<EntryBOSRule> _logger;
    
    public string RuleName => "EntryBOSRule";
    public int Priority => 25; // Same priority as CHoCH rule
    
    public EntryBOSRule(ILogger<EntryBOSRule> logger)
    {
        _logger = logger;
    }
    
    public Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        // Only process signals from EntryBOS webhook
        if (signal.Type?.ToLower() != "entrybos")
        {
            return Task.FromResult(RuleResult.Success("Not an EntryBOS signal"));
        }
        
        _logger.LogInformation("Processing EntryBOS signal: {Symbol} {Action} at {Price}", 
            signal.Symbol, signal.Action, signal.Price);
        
        // Get recent signals for the same symbol
        var recentSignals = context.ExistingSignals
            .Where(s => s.Symbol == signal.Symbol 
                     && s.Id != signal.Id)
            .OrderByDescending(s => s.Timestamp)
            .Take(10)
            .ToList();
        
        var recentBOS = recentSignals.Where(s => s.Type?.ToLower() == "entrybos").ToList();
        var recentCHoCH = recentSignals.Where(s => s.Type?.ToLower() == "entrychoch").ToList();
        
        // Rule 1: BOS should happen in the same direction as the trend
        // Check if there's a recent CHoCH in opposite direction (would invalidate BOS)
        var oppositeAction = signal.Action == "BUY" ? "SELL" : "BUY";
        var recentOppositeChoch = recentCHoCH
            .Where(s => s.Action == oppositeAction)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefault();
        
        if (recentOppositeChoch != null)
        {
            var timeSinceChoch = signal.Timestamp - recentOppositeChoch.Timestamp;
            
            // If there's a recent opposite CHoCH within 30 minutes, log warning
            if (timeSinceChoch.TotalMinutes < 30)
            {
                _logger.LogWarning("BOS {Action} conflicts with recent opposite CHoCH {OppositeAction} {Minutes} minutes ago - Potential trend change",
                    signal.Action, oppositeAction, timeSinceChoch.TotalMinutes);
                
                // Don't reject - trend might be changing, but signal is still recorded
            }
        }
        
        // Rule 2: Check for same-direction BOS to confirm trend continuation
        var sameDirectionBOS = recentBOS
            .Where(s => s.Action == signal.Action)
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefault();
        
        if (sameDirectionBOS != null)
        {
            var timeSinceLastBOS = signal.Timestamp - sameDirectionBOS.Timestamp;
            
            // If less than 15 minutes, might be duplicate or too frequent
            if (timeSinceLastBOS.TotalMinutes < 15)
            {
                _logger.LogWarning("BOS signals very frequent: {Minutes} minutes since last BOS - Strong momentum",
                    timeSinceLastBOS.TotalMinutes);
                
                // Don't reject - could be strong trending market
            }
            
            // If within 2 hours, this confirms strong trend
            if (timeSinceLastBOS.TotalHours < 2)
            {
                _logger.LogInformation("Strong trend confirmed: Multiple BOS {Action} within 2 hours",
                    signal.Action);
                
                // Inherit swing from previous BOS for consistency
                if (string.IsNullOrEmpty(signal.Swing) && !string.IsNullOrEmpty(sameDirectionBOS.Swing))
                {
                    signal.Swing = sameDirectionBOS.Swing;
                    _logger.LogInformation("Swing inherited from previous BOS: {Swing}", signal.Swing);
                }
            }
        }
        
        // Rule 3: Validate price broke a significant level (using swing)
        if (!string.IsNullOrEmpty(signal.Swing))
        {
            var swingPrice = double.Parse(signal.Swing);
            var priceDiff = signal.Action == "BUY" 
                ? signal.Price - swingPrice  // BUY should break above
                : swingPrice - signal.Price; // SELL should break below
            
            // BOS must break the swing level
            if (priceDiff <= 0)
            {
                _logger.LogWarning("BOS {Action} did not break swing level {Swing}. Current: {Price} - Potential false signal",
                    signal.Action, swingPrice, signal.Price);
                
                // Don't reject - might be valid in different timeframe or interpretation
            }
            
            var breakPercent = (Math.Abs(priceDiff) / swingPrice) * 100;
            _logger.LogInformation("BOS broke structure by {Percent}%", breakPercent);
            
            // If break is too large (>1%), might be false breakout or gap
            if (breakPercent > 1.0)
            {
                _logger.LogWarning("BOS break is very large: {Percent}% - Verify legitimacy", breakPercent);
            }
        }
        else
        {
            // If no swing provided, try to detect from recent price action
            if (recentSignals.Any())
            {
                var prices = recentSignals.Take(5).Select(s => s.Price).ToList();
                
                if (signal.Action == "BUY")
                {
                    // BUY BOS should break recent high
                    var recentHigh = prices.Max();
                    if (signal.Price > recentHigh)
                    {
                        signal.Swing = recentHigh.ToString("F5");
                        _logger.LogInformation("BUY BOS broke recent high: {Swing}", signal.Swing);
                    }
                }
                else // SELL
                {
                    // SELL BOS should break recent low
                    var recentLow = prices.Min();
                    if (signal.Price < recentLow)
                    {
                        signal.Swing = recentLow.ToString("F5");
                        _logger.LogInformation("SELL BOS broke recent low: {Swing}", signal.Swing);
                    }
                }
            }
        }
        
        // Rule 4: Check market alignment - BOS in same direction strengthens setup
        var recentSameDirectionSignals = recentSignals
            .Where(s => s.Action == signal.Action && s.Timestamp > context.ProcessingTime.AddHours(-4))
            .Count();
        
        if (recentSameDirectionSignals >= 2)
        {
            _logger.LogInformation("Strong directional bias: {Count} {Action} signals in 4 hours",
                recentSameDirectionSignals + 1, signal.Action);
        }
        
        // Rule 5: Auto-resolve old BOS signals (longer timeframe than CHoCH)
        foreach (var oldSignal in recentBOS.Where(s => s.Resolved == 0))
        {
            var age = context.ProcessingTime - oldSignal.Timestamp;
            if (age.TotalHours > 8) // BOS signals valid for longer (8 hours)
            {
                oldSignal.Resolved = 1;
                _logger.LogInformation("Auto-resolved old BOS signal: {Symbol} {Action}, age: {Hours}h",
                    oldSignal.Symbol, oldSignal.Action, age.TotalHours);
            }
        }
        
        _logger.LogInformation("EntryBOS signal validated successfully: {Symbol} {Action}",
            signal.Symbol, signal.Action);
        
        return Task.FromResult(RuleResult.Success("BOS signal validated"));
    }
}
