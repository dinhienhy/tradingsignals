using TradingSignalsApi.Models;

namespace TradingSignalsApi.BusinessRules;

/// <summary>
/// Engine that executes business rules on trading signals
/// </summary>
public class SignalRuleEngine
{
    private readonly ILogger<SignalRuleEngine> _logger;
    private readonly List<ISignalRule> _rules;
    
    public SignalRuleEngine(IEnumerable<ISignalRule> rules, ILogger<SignalRuleEngine> logger)
    {
        _logger = logger;
        _rules = rules.OrderBy(r => r.Priority).ToList();
        
        _logger.LogInformation("SignalRuleEngine initialized with {Count} rules", _rules.Count);
    }
    
    /// <summary>
    /// Execute all rules on a signal
    /// </summary>
    public async Task<SignalValidationResult> ValidateAsync(ActiveTradingSignal signal, SignalContext context)
    {
        var result = new SignalValidationResult { IsValid = true };
        
        foreach (var rule in _rules)
        {
            try
            {
                _logger.LogDebug("Executing rule: {RuleName} on signal {Symbol} {Action}", 
                    rule.RuleName, signal.Symbol, signal.Action);
                
                var ruleResult = await rule.ExecuteAsync(signal, context);
                
                result.RuleResults.Add(rule.RuleName, ruleResult);
                
                if (!ruleResult.IsValid)
                {
                    result.IsValid = false;
                    result.FailedRules.Add(rule.RuleName);
                    _logger.LogWarning("Rule {RuleName} failed: {Message}", rule.RuleName, ruleResult.Message);
                }
                
                if (!ruleResult.ShouldContinue)
                {
                    _logger.LogInformation("Rule {RuleName} stopped execution chain", rule.RuleName);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing rule {RuleName}", rule.RuleName);
                result.Errors.Add($"{rule.RuleName}: {ex.Message}");
            }
        }
        
        return result;
    }
}

/// <summary>
/// Result of validation by the rule engine
/// </summary>
public class SignalValidationResult
{
    public bool IsValid { get; set; }
    public Dictionary<string, RuleResult> RuleResults { get; set; } = new();
    public List<string> FailedRules { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    
    public string GetSummary()
    {
        if (IsValid)
            return "All rules passed";
        
        return $"Failed rules: {string.Join(", ", FailedRules)}";
    }
}
