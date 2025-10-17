using Microsoft.EntityFrameworkCore;
using TradingSignalsApi.Data;
using TradingSignalsApi.Models;

namespace TradingSignalsApi.Services;

/// <summary>
/// Background service that monitors and processes trading signals every minute
/// </summary>
public class SignalMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SignalMonitoringService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
    
    public SignalMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<SignalMonitoringService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Signal Monitoring Service started");
        
        // Wait a bit before first run
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSignalsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Signal Monitoring Service");
            }
            
            // Wait 1 minute before next run
            await Task.Delay(_interval, stoppingToken);
        }
        
        _logger.LogInformation("Signal Monitoring Service stopped");
    }
    
    private async Task ProcessSignalsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        _logger.LogInformation("Starting signal processing cycle...");
        
        // Get all active signals that are not resolved
        var activeSignals = await context.ActiveTradingSignals
            .Where(s => !s.Resolved)
            .OrderBy(s => s.Type)
            .ThenByDescending(s => s.Timestamp)
            .ToListAsync();
        
        _logger.LogInformation("Found {Count} active unresolved signals", activeSignals.Count);
        
        if (activeSignals.Count == 0)
        {
            return;
        }
        
        int resolvedCount = 0;
        int updatedCount = 0;
        
        // Group signals by type for processing
        var signalsByType = activeSignals.GroupBy(s => s.Type?.ToLower() ?? "unknown");
        
        foreach (var typeGroup in signalsByType)
        {
            var type = typeGroup.Key;
            var signals = typeGroup.ToList();
            
            _logger.LogInformation("Processing {Count} signals for type: {Type}", signals.Count, type);
            
            (int r, int u) result = type switch
            {
                "entrychoch" => await ProcessCHoCHSignalsAsync(context, signals),
                "entrybos" => await ProcessBOSSignalsAsync(context, signals),
                _ => await ProcessGenericSignalsAsync(context, signals)
            };
            
            resolvedCount += result.r;
            updatedCount += result.u;
        }
        
        // Save all changes
        if (resolvedCount > 0 || updatedCount > 0)
        {
            await context.SaveChangesAsync();
            _logger.LogInformation("Processing complete. Resolved: {Resolved}, Updated: {Updated}", 
                resolvedCount, updatedCount);
        }
        else
        {
            _logger.LogInformation("Processing complete. No changes needed.");
        }
    }
    
    /// <summary>
    /// Process EntryCHoCH signals
    /// </summary>
    private Task<(int resolved, int updated)> ProcessCHoCHSignalsAsync(
        AppDbContext context, 
        List<ActiveTradingSignal> signals)
    {
        var now = DateTime.UtcNow;
        int resolvedCount = 0;
        int updatedCount = 0;
        
        // Group by symbol
        var bySymbol = signals.GroupBy(s => s.Symbol);
        
        foreach (var symbolGroup in bySymbol)
        {
            var symbolSignals = symbolGroup.OrderByDescending(s => s.Timestamp).ToList();
            
            // Rule 1: Auto-expire CHoCH signals older than 4 hours
            foreach (var signal in symbolSignals)
            {
                var age = now - signal.Timestamp;
                if (age.TotalHours > 4)
                {
                    signal.Resolved = true;
                    resolvedCount++;
                    _logger.LogInformation("Auto-resolved old CHoCH: {Symbol} {Action}, age: {Hours}h", 
                        signal.Symbol, signal.Action, age.TotalHours);
                }
            }
            
            // Rule 2: Check for trend reversal - mark opposite direction as resolved
            var recentSignals = symbolSignals.Take(3).ToList();
            if (recentSignals.Count >= 2)
            {
                var newest = recentSignals[0];
                for (int i = 1; i < recentSignals.Count; i++)
                {
                    var older = recentSignals[i];
                    
                    // If opposite direction within 1 hour, resolve the older one
                    if (newest.Action != older.Action && !older.Resolved)
                    {
                        var timeDiff = newest.Timestamp - older.Timestamp;
                        if (timeDiff.TotalHours < 1)
                        {
                            older.Resolved = true;
                            resolvedCount++;
                            _logger.LogInformation("Resolved CHoCH due to reversal: {Symbol} {Action}", 
                                older.Symbol, older.Action);
                        }
                    }
                }
            }
        }
        
        return Task.FromResult((resolvedCount, updatedCount));
    }
    
    /// <summary>
    /// Process EntryBOS signals
    /// </summary>
    private async Task<(int resolved, int updated)> ProcessBOSSignalsAsync(
        AppDbContext context, 
        List<ActiveTradingSignal> signals)
    {
        var now = DateTime.UtcNow;
        int resolvedCount = 0;
        int updatedCount = 0;
        
        // Group by symbol
        var bySymbol = signals.GroupBy(s => s.Symbol);
        
        foreach (var symbolGroup in bySymbol)
        {
            var symbolSignals = symbolGroup.OrderByDescending(s => s.Timestamp).ToList();
            
            // Rule 1: Auto-expire BOS signals older than 8 hours
            foreach (var signal in symbolSignals)
            {
                var age = now - signal.Timestamp;
                if (age.TotalHours > 8)
                {
                    signal.Resolved = true;
                    resolvedCount++;
                    _logger.LogInformation("Auto-resolved old BOS: {Symbol} {Action}, age: {Hours}h", 
                        signal.Symbol, signal.Action, age.TotalHours);
                }
            }
            
            // Rule 2: Check for conflicting CHoCH signals
            var allSymbolSignals = await context.ActiveTradingSignals
                .Where(s => s.Symbol == symbolGroup.Key && !s.Resolved)
                .ToListAsync();
            
            var chochSignals = allSymbolSignals
                .Where(s => s.Type?.ToLower() == "entrychoch")
                .OrderByDescending(s => s.Timestamp)
                .ToList();
            
            foreach (var bosSignal in symbolSignals.Where(s => !s.Resolved))
            {
                // Check if there's a recent opposite CHoCH
                var oppositeAction = bosSignal.Action == "BUY" ? "SELL" : "BUY";
                var oppositeChoch = chochSignals
                    .Where(c => c.Action == oppositeAction && c.Timestamp > bosSignal.Timestamp.AddMinutes(-30))
                    .FirstOrDefault();
                
                if (oppositeChoch != null)
                {
                    bosSignal.Resolved = true;
                    resolvedCount++;
                    _logger.LogInformation("Resolved BOS due to opposite CHoCH: {Symbol} {Action}", 
                        bosSignal.Symbol, bosSignal.Action);
                }
            }
        }
        
        return (resolvedCount, updatedCount);
    }
    
    /// <summary>
    /// Process generic signals (other types)
    /// </summary>
    private Task<(int resolved, int updated)> ProcessGenericSignalsAsync(
        AppDbContext context, 
        List<ActiveTradingSignal> signals)
    {
        var now = DateTime.UtcNow;
        int resolvedCount = 0;
        int updatedCount = 0;
        
        // Auto-expire signals older than 24 hours
        foreach (var signal in signals)
        {
            var age = now - signal.Timestamp;
            if (age.TotalHours > 24)
            {
                signal.Resolved = true;
                resolvedCount++;
                _logger.LogInformation("Auto-resolved old signal: {Symbol} {Action} ({Type}), age: {Hours}h", 
                    signal.Symbol, signal.Action, signal.Type, age.TotalHours);
            }
        }
        
        return Task.FromResult((resolvedCount, updatedCount));
    }
}

