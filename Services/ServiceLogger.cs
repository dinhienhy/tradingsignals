using TradingSignalsApi.Data;
using TradingSignalsApi.Models;
using System.Text.Json;

namespace TradingSignalsApi.Services;

/// <summary>
/// Helper service for logging service activities to database
/// </summary>
public class ServiceLogger
{
    private readonly IServiceProvider _serviceProvider;

    public ServiceLogger(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task LogAsync(
        string level,
        string source,
        string action,
        string message,
        string? symbol = null,
        string? signalType = null,
        object? data = null,
        int? executionTimeMs = null)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var log = new ServiceLog
            {
                Level = level,
                Source = source,
                Action = action,
                Message = message,
                Symbol = symbol,
                SignalType = signalType,
                Data = data != null ? JsonSerializer.Serialize(data) : null,
                ExecutionTimeMs = executionTimeMs,
                Timestamp = DateTime.UtcNow
            };

            await context.ServiceLogs.AddAsync(log);
            await context.SaveChangesAsync();
        }
        catch
        {
            // Silently fail to avoid breaking the main service
        }
    }

    public Task InfoAsync(string source, string action, string message, string? symbol = null, string? signalType = null, object? data = null, int? executionTimeMs = null)
        => LogAsync("INFO", source, action, message, symbol, signalType, data, executionTimeMs);

    public Task WarningAsync(string source, string action, string message, string? symbol = null, string? signalType = null, object? data = null, int? executionTimeMs = null)
        => LogAsync("WARNING", source, action, message, symbol, signalType, data, executionTimeMs);

    public Task ErrorAsync(string source, string action, string message, string? symbol = null, string? signalType = null, object? data = null, int? executionTimeMs = null)
        => LogAsync("ERROR", source, action, message, symbol, signalType, data, executionTimeMs);

    public Task DebugAsync(string source, string action, string message, string? symbol = null, string? signalType = null, object? data = null, int? executionTimeMs = null)
        => LogAsync("DEBUG", source, action, message, symbol, signalType, data, executionTimeMs);
}
