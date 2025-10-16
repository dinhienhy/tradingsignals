using TradingSignalsApi.Models;
using TradingSignalsApi.BusinessRules;

namespace TradingSignalsApi.Services;

/// <summary>
/// Service interface for processing active trading signals with business rules
/// </summary>
public interface IActiveSignalProcessor
{
    /// <summary>
    /// Process a new signal through business rules
    /// </summary>
    Task<ProcessingResult> ProcessSignalAsync(ActiveTradingSignal signal);
    
    /// <summary>
    /// Run maintenance tasks (expire old signals, etc.)
    /// </summary>
    Task RunMaintenanceAsync();
}

/// <summary>
/// Result of signal processing
/// </summary>
public class ProcessingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SignalValidationResult? ValidationResult { get; set; }
    public ActiveTradingSignal? ProcessedSignal { get; set; }
}
