using TradingSignalsApi.Models;

namespace TradingSignalsApi.BusinessRules.Rules;

/// <summary>
/// Prevents duplicate signals for the same symbol and action within a time window
/// </summary>
public class DuplicateSignalRule : ISignalRule
{
    private readonly ILogger<DuplicateSignalRule> _logger;
    private readonly TimeSpan _duplicateWindow = TimeSpan.FromMinutes(5);
    
    public string RuleName => "DuplicateSignalRule";
    public int Priority => 10;
    
    public DuplicateSignalRule(ILogger<DuplicateSignalRule> logger)
    {
        _logger = logger;
    }
    
    public Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        var cutoffTime = context.ProcessingTime.AddMinutes(-_duplicateWindow.TotalMinutes);
        
        var duplicate = context.ExistingSignals
            .Where(s => s.Symbol == signal.Symbol 
                     && s.Action == signal.Action
                     && s.Timestamp > cutoffTime
                     && s.Id != signal.Id)
            .FirstOrDefault();
        
        if (duplicate != null)
        {
            _logger.LogWarning("Duplicate signal detected: {Symbol} {Action} within {Minutes} minutes", 
                signal.Symbol, signal.Action, _duplicateWindow.TotalMinutes);
            
            return Task.FromResult(RuleResult.Failure(
                $"Duplicate signal within {_duplicateWindow.TotalMinutes} minutes", 
                shouldContinue: false));
        }
        
        return Task.FromResult(RuleResult.Success());
    }
}
