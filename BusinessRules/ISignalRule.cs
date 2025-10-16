using TradingSignalsApi.Models;

namespace TradingSignalsApi.BusinessRules;

/// <summary>
/// Interface for business rules that validate and process trading signals
/// </summary>
public interface ISignalRule
{
    /// <summary>
    /// Rule name for logging and identification
    /// </summary>
    string RuleName { get; }
    
    /// <summary>
    /// Priority - lower number = higher priority (runs first)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Execute the rule on a signal
    /// </summary>
    /// <param name="signal">The signal to process</param>
    /// <param name="context">Additional context (existing signals, etc.)</param>
    /// <returns>Rule execution result</returns>
    Task<RuleResult> ExecuteAsync(ActiveTradingSignal signal, SignalContext context);
}

/// <summary>
/// Result of rule execution
/// </summary>
public class RuleResult
{
    public bool IsValid { get; set; }
    public bool ShouldContinue { get; set; } = true;
    public string? Message { get; set; }
    public Dictionary<string, object>? Data { get; set; }
    
    public static RuleResult Success(string? message = null)
        => new() { IsValid = true, Message = message };
    
    public static RuleResult Failure(string message, bool shouldContinue = true)
        => new() { IsValid = false, Message = message, ShouldContinue = shouldContinue };
    
    public static RuleResult Skip(string message)
        => new() { IsValid = true, Message = message, ShouldContinue = false };
}

/// <summary>
/// Context provided to rules for decision making
/// </summary>
public class SignalContext
{
    public List<ActiveTradingSignal> ExistingSignals { get; set; } = new();
    public DateTime ProcessingTime { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
