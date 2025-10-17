using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingSignalsApi.Models;

/// <summary>
/// Service activity log for monitoring and debugging
/// </summary>
public class ServiceLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Log level: INFO, WARNING, ERROR, DEBUG
    /// </summary>
    public string Level { get; set; } = "INFO";
    
    /// <summary>
    /// Source component: SignalMonitoring, MetaApi, Webhook, etc.
    /// </summary>
    public string Source { get; set; } = string.Empty;
    
    /// <summary>
    /// Action performed: ProcessCycle, FetchPrice, ResolveSignal, etc.
    /// </summary>
    public string Action { get; set; } = string.Empty;
    
    /// <summary>
    /// Related symbol (optional)
    /// </summary>
    public string? Symbol { get; set; }
    
    /// <summary>
    /// Related signal type (optional)
    /// </summary>
    public string? SignalType { get; set; }
    
    /// <summary>
    /// Log message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Additional data in JSON format (optional)
    /// </summary>
    public string? Data { get; set; }
    
    /// <summary>
    /// Execution time in milliseconds (optional)
    /// </summary>
    public int? ExecutionTimeMs { get; set; }
}
