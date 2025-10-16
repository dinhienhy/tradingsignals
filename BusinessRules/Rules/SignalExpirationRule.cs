using TradingSignalsApi.Models;
using TradingSignalsApi.Data;
using Microsoft.EntityFrameworkCore;

namespace TradingSignalsApi.BusinessRules.Rules;

/// <summary>
/// Auto-resolves expired signals
/// </summary>
public class SignalExpirationRule : ISignalRule
{
    private readonly ILogger<SignalExpirationRule> _logger;
    private readonly TimeSpan _expirationPeriod = TimeSpan.FromHours(24);
    
    public string RuleName => "SignalExpirationRule";
    public int Priority => 50; // Run later
    
    public SignalExpirationRule(ILogger<SignalExpirationRule> logger)
    {
        _logger = logger;
    }
    
    public Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context)
    {
        var signalAge = context.ProcessingTime - signal.Timestamp;
        
        if (signalAge > _expirationPeriod && signal.Resolved == 0)
        {
            _logger.LogInformation("Signal expired: {Symbol} {Action}, age: {Hours} hours", 
                signal.Symbol, signal.Action, signalAge.TotalHours);
            
            signal.Resolved = 1; // Mark as resolved
            
            return Task.FromResult(RuleResult.Success($"Signal auto-expired after {signalAge.TotalHours:F1} hours"));
        }
        
        return Task.FromResult(RuleResult.Success());
    }
}
