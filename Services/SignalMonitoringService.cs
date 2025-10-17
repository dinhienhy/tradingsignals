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
    private readonly MetaApiService _metaApiService;
    private readonly ServiceLogger _serviceLogger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(1);
    
    public SignalMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<SignalMonitoringService> logger,
        MetaApiService metaApiService,
        ServiceLogger serviceLogger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _metaApiService = metaApiService;
        _serviceLogger = serviceLogger;
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
        var cycleStart = DateTime.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        _logger.LogInformation("Starting signal processing cycle...");
        await _serviceLogger.InfoAsync("SignalMonitoring", "CycleStart", "Starting new processing cycle");
        
        // Get all active signals that are not resolved
        // Use ResolvedAsInt because Resolved is NotMapped
        var activeSignals = await context.ActiveTradingSignals
            .Where(s => s.ResolvedAsInt == 0)
            .OrderBy(s => s.Type)
            .ThenByDescending(s => s.Timestamp)
            .ToListAsync();
        
        _logger.LogInformation("Found {Count} active unresolved signals", activeSignals.Count);
        await _serviceLogger.InfoAsync("SignalMonitoring", "QuerySignals", 
            $"Found {activeSignals.Count} active unresolved signals",
            data: new { Count = activeSignals.Count, QueryTimeMs = stopwatch.ElapsedMilliseconds });
        
        if (activeSignals.Count == 0)
        {
            await _serviceLogger.DebugAsync("SignalMonitoring", "CycleComplete", 
                "No signals to process", 
                executionTimeMs: (int)stopwatch.ElapsedMilliseconds);
            return;
        }
        
        int resolvedCount = 0;
        int updatedCount = 0;
        
        // Group signals by type for processing
        var signalsByType = activeSignals.GroupBy(s => s.Type?.ToLower() ?? "unknown");
        
        await _serviceLogger.InfoAsync("SignalMonitoring", "SignalTypes", 
            $"Processing {signalsByType.Count()} different signal types",
            data: new {
                Types = signalsByType.Select(g => new { Type = g.Key, Count = g.Count() }).ToList()
            });
        
        foreach (var typeGroup in signalsByType)
        {
            var signalType = typeGroup.Key;
            var typeSignals = typeGroup.ToList();
            
            await _serviceLogger.InfoAsync("SignalMonitoring", "ProcessType", 
                $"Processing {typeSignals.Count} signals of type: {signalType}",
                data: new { SignalType = signalType, Count = typeSignals.Count });
            
            // Process different signal types
            (int r, int u) result = signalType switch
            {
                "entrychoch" => await ProcessCHoCHSignalsAsync(context, typeSignals),
                "entrybos" => await ProcessBOSSignalsAsync(context, typeSignals),
                _ => await ProcessGenericSignalsAsync(context, typeSignals)
            };
            
            await _serviceLogger.InfoAsync("SignalMonitoring", "TypeComplete", 
                $"Completed processing {signalType}: Resolved={result.r}, Updated={result.u}",
                data: new { SignalType = signalType, Resolved = result.r, Updated = result.u });
            
            resolvedCount += result.r;
            updatedCount += result.u;
        }
        
        // Save all changes
        if (resolvedCount > 0 || updatedCount > 0)
        {
            await context.SaveChangesAsync();
            _logger.LogInformation("Processing complete. Resolved: {Resolved}, Updated: {Updated}", 
                resolvedCount, updatedCount);
            
            stopwatch.Stop();
            await _serviceLogger.InfoAsync("SignalMonitoring", "CycleComplete", 
                $"Processing complete. Resolved: {resolvedCount}, Updated: {updatedCount}",
                data: new { Resolved = resolvedCount, Updated = updatedCount, TotalTimeMs = stopwatch.ElapsedMilliseconds });
        }
        else
        {
            _logger.LogInformation("Processing complete. No changes needed.");
            stopwatch.Stop();
            await _serviceLogger.DebugAsync("SignalMonitoring", "CycleComplete", 
                "Processing complete. No changes needed.",
                executionTimeMs: (int)stopwatch.ElapsedMilliseconds);
        }
    }
    
    /// <summary>
    /// Process EntryCHoCH signals
    /// </summary>
    private async Task<(int resolved, int updated)> ProcessCHoCHSignalsAsync(
        AppDbContext context, 
        List<ActiveTradingSignal> signals)
    {
        int resolvedCount = 0;
        int updatedCount = 0;
        var now = DateTime.UtcNow;
        
        _logger.LogInformation("Processing {Count} signals for type: {Type}", 
            signals.Count, signals.First().Type?.ToLower() ?? "unknown");
        
        await _serviceLogger.InfoAsync("CHoCH", "ProcessStart", 
            $"Starting to process {signals.Count} CHoCH signals",
            data: new { 
                TotalSignals = signals.Count,
                Symbols = signals.Select(s => s.Symbol).Distinct().ToList()
            });
        
        // Group by symbol
        var signalsBySymbol = signals.GroupBy(s => s.Symbol);
        
        foreach (var symbolGroup in signalsBySymbol)
        {
            var symbol = symbolGroup.Key;
            var symbolSignals = symbolGroup.OrderByDescending(s => s.Timestamp).ToList();
            
            await _serviceLogger.DebugAsync("CHoCH", "ProcessSymbol", 
                $"Processing {symbolSignals.Count} CHoCH signals for {symbol}",
                symbol: symbol,
                data: new {
                    SignalCount = symbolSignals.Count,
                    Actions = symbolSignals.Select(s => s.Action).ToList(),
                    Timestamps = symbolSignals.Select(s => s.Timestamp).ToList()
                });
            
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
            
            // Rule 3: Check price against BOS Swing (NEW LOGIC)
            // Get current price from MetaApi
            var priceStart = System.Diagnostics.Stopwatch.StartNew();
            var currentPrice = await _metaApiService.GetCurrentPriceAsync(symbol);
            priceStart.Stop();
            
            if (currentPrice != null)
            {
                await _serviceLogger.DebugAsync("MetaApi", "FetchPrice", 
                    $"Price fetched for {symbol}: Bid={currentPrice.Bid}, Ask={currentPrice.Ask}, Mid={currentPrice.MidPrice}",
                    symbol: symbol,
                    data: new { 
                        Bid = currentPrice.Bid, 
                        Ask = currentPrice.Ask, 
                        MidPrice = currentPrice.MidPrice,
                        FetchTimeMs = priceStart.ElapsedMilliseconds 
                    });
                // Get all BOS signals for this symbol
                var bosSignals = await context.ActiveTradingSignals
                    .Where(s => s.Symbol == symbol 
                             && s.Type != null && s.Type.ToLower() == "entrybos" 
                             && s.ResolvedAsInt == 0
                             && s.Swing.HasValue)
                    .OrderByDescending(s => s.Timestamp)
                    .ToListAsync();
                
                await _serviceLogger.DebugAsync("CHoCH", "QueryBOS", 
                    $"Found {bosSignals.Count} active BOS signals for {symbol}",
                    symbol: symbol,
                    data: new {
                        BOSCount = bosSignals.Count,
                        BOSSwings = bosSignals.Select(b => new { b.Action, b.Swing, b.Timestamp }).ToList()
                    });
                
                if (bosSignals.Any())
                {
                    // Check each CHoCH signal against BOS swings
                    foreach (var chochSignal in symbolSignals.Where(s => !s.Resolved))
                    {
                        // Find BOS signals that came BEFORE this CHoCH
                        var relevantBOS = bosSignals
                            .Where(b => b.Timestamp < chochSignal.Timestamp)
                            .FirstOrDefault();
                        
                        if (relevantBOS != null && relevantBOS.Swing.HasValue)
                        {
                            var price = currentPrice.MidPrice;
                            var bosSwing = relevantBOS.Swing.Value;
                            
                            await _serviceLogger.DebugAsync("CHoCH", "CheckPrice", 
                                $"Checking CHoCH {chochSignal.Action} {symbol}: Price={price} vs BOS Swing={bosSwing}",
                                symbol: symbol,
                                signalType: "EntryCHoCH",
                                data: new {
                                    CHoCHId = chochSignal.Id,
                                    CHoCHAction = chochSignal.Action,
                                    CHoCHTimestamp = chochSignal.Timestamp,
                                    CurrentPrice = price,
                                    BOSSwing = bosSwing,
                                    BOSId = relevantBOS.Id,
                                    BOSTimestamp = relevantBOS.Timestamp,
                                    PriceDifference = chochSignal.Action == "BUY" ? price - bosSwing : bosSwing - price
                                });
                            
                            bool shouldResolve = false;
                            
                            // CHoCH BUY: Resolve if price goes BELOW BOS Swing
                            if (chochSignal.Action == "BUY" && price < bosSwing)
                            {
                                shouldResolve = true;
                                _logger.LogInformation(
                                    "CHoCH BUY {Symbol} resolved: Price {Price} broke below BOS Swing {Swing}",
                                    symbol, price, bosSwing);
                                    
                                await _serviceLogger.InfoAsync("CHoCH", "PriceBreakResolved", 
                                    $"CHoCH BUY {symbol} resolved: Price {price} broke below BOS Swing {bosSwing}",
                                    symbol: symbol,
                                    signalType: "EntryCHoCH",
                                    data: new { 
                                        Action = "BUY",
                                        CurrentPrice = price,
                                        BOSSwing = bosSwing,
                                        Difference = bosSwing - price,
                                        SignalId = chochSignal.Id
                                    });
                            }
                            // CHoCH SELL: Resolve if price goes ABOVE BOS Swing
                            else if (chochSignal.Action == "SELL" && price > bosSwing)
                            {
                                shouldResolve = true;
                                _logger.LogInformation(
                                    "CHoCH SELL {Symbol} resolved: Price {Price} broke above BOS Swing {Swing}",
                                    symbol, price, bosSwing);
                                    
                                await _serviceLogger.InfoAsync("CHoCH", "PriceBreakResolved", 
                                    $"CHoCH SELL {symbol} resolved: Price {price} broke above BOS Swing {bosSwing}",
                                    symbol: symbol,
                                    signalType: "EntryCHoCH",
                                    data: new { 
                                        Action = "SELL",
                                        CurrentPrice = price,
                                        BOSSwing = bosSwing,
                                        Difference = price - bosSwing,
                                        SignalId = chochSignal.Id
                                    });
                            }
                            
                            if (shouldResolve)
                            {
                                chochSignal.Resolved = true;
                                resolvedCount++;
                            }
                            else
                            {
                                await _serviceLogger.DebugAsync("CHoCH", "PriceNotBroken", 
                                    $"CHoCH {chochSignal.Action} {symbol} still active: Price has not broken swing level",
                                    symbol: symbol,
                                    signalType: "EntryCHoCH",
                                    data: new {
                                        CHoCHAction = chochSignal.Action,
                                        CurrentPrice = price,
                                        BOSSwing = bosSwing,
                                        RequiredCondition = chochSignal.Action == "BUY" ? "Price < Swing" : "Price > Swing"
                                    });
                            }
                        }
                        else
                        {
                            await _serviceLogger.WarningAsync("CHoCH", "NoBOSFound", 
                                $"No relevant BOS signal found for CHoCH {chochSignal.Action} {symbol}",
                                symbol: symbol,
                                signalType: "EntryCHoCH",
                                data: new {
                                    CHoCHId = chochSignal.Id,
                                    CHoCHTimestamp = chochSignal.Timestamp,
                                    TotalBOSCount = bosSignals.Count
                                });
                        }
                    }
                }
                else
                {
                    await _serviceLogger.InfoAsync("CHoCH", "NoBOSSignals", 
                        $"No active BOS signals with Swing found for {symbol}, cannot check price break",
                        symbol: symbol,
                        signalType: "EntryCHoCH");
                }
            }
            else
            {
                _logger.LogWarning("Could not get current price for {Symbol}, skipping price check", symbol);
                await _serviceLogger.WarningAsync("MetaApi", "FetchPriceFailed", 
                    $"Could not get current price for {symbol}, skipping price check",
                    symbol: symbol,
                    data: new { FetchTimeMs = priceStart.ElapsedMilliseconds });
            }
        }
        
        return (resolvedCount, updatedCount);
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
        
        await _serviceLogger.InfoAsync("BOS", "ProcessStart", 
            $"Starting to process {signals.Count} BOS signals",
            data: new { 
                TotalSignals = signals.Count,
                Symbols = signals.Select(s => s.Symbol).Distinct().ToList()
            });
        
        // Group by symbol
        var bySymbol = signals.GroupBy(s => s.Symbol);
        
        foreach (var symbolGroup in bySymbol)
        {
            var symbolSignals = symbolGroup.OrderByDescending(s => s.Timestamp).ToList();
            
            await _serviceLogger.DebugAsync("BOS", "ProcessSymbol", 
                $"Processing {symbolSignals.Count} BOS signals for {symbolGroup.Key}",
                symbol: symbolGroup.Key,
                data: new {
                    SignalCount = symbolSignals.Count,
                    Actions = symbolSignals.Select(s => s.Action).ToList(),
                    Swings = symbolSignals.Select(s => s.Swing).ToList()
                });
            
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
                .Where(s => s.Symbol == symbolGroup.Key && s.ResolvedAsInt == 0)
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

