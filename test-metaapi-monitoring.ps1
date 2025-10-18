# Test MetaAPI integration with SignalMonitoringService
$baseUrl = "https://tradingsignals-ae14b4a15912.herokuapp.com"
$apiKey = "LaxArXqne4NxVstvl9PdDNvj2OJ00vvX"

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Test MetaAPI Signal Monitoring" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Create webhook config for test
Write-Host "Step 1: Ensure webhook config exists..." -ForegroundColor Yellow
try {
    $webhooks = Invoke-RestMethod -Uri "$baseUrl/config/webhooks" -Headers @{"ConfigApiKey"=$apiKey} -Method GET
    $testWebhook = $webhooks | Where-Object { $_.path -eq "TestCHoCH" } | Select-Object -First 1
    
    if (-not $testWebhook) {
        Write-Host "Creating webhook config..." -ForegroundColor Gray
        $webhookConfig = @{
            path = "TestCHoCH"
            secret = "test-secret-123"
            description = "Test CHoCH for MetaAPI monitoring"
        } | ConvertTo-Json
        
        Invoke-RestMethod -Uri "$baseUrl/config/webhooks" -Headers @{"ConfigApiKey"=$apiKey; "Content-Type"="application/json"} -Method POST -Body $webhookConfig | Out-Null
        Write-Host "✅ Webhook config created" -ForegroundColor Green
    } else {
        Write-Host "✅ Webhook config exists" -ForegroundColor Green
    }
} catch {
    Write-Host "⚠️ Could not manage webhook config: $_" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Step 2: Send CHoCH signal for XAUUSD..." -ForegroundColor Yellow

# Create CHoCH signal
$chochPayload = @{
    secret = "test-secret-123"
    symbol = "XAUUSD"
    action = "BUY"
    price = 2650.00
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

try {
    $chochResponse = Invoke-RestMethod -Uri "$baseUrl/webhook/EntryCHoCH" -Method POST -Body $chochPayload -ContentType "application/json"
    Write-Host "✅ CHoCH signal created:" -ForegroundColor Green
    Write-Host "  Signal ID: $($chochResponse.signalId)" -ForegroundColor White
    Write-Host "  Active Signal ID: $($chochResponse.activeSignalId)" -ForegroundColor White
    Write-Host "  Symbol: $($chochResponse.symbol)" -ForegroundColor White
    Write-Host "  Price: $($chochResponse.price)" -ForegroundColor White
} catch {
    Write-Host "❌ Failed to create CHoCH signal: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 3: Send BOS signal with Swing for XAUUSD..." -ForegroundColor Yellow

# Create BOS signal with Swing
$bosPayload = @{
    secret = "test-secret-123"
    symbol = "XAUUSD"
    action = "BUY"
    price = 2655.00
    swing = 2645.00
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

try {
    $bosResponse = Invoke-RestMethod -Uri "$baseUrl/webhook/EntryBOS" -Method POST -Body $bosPayload -ContentType "application/json"
    Write-Host "✅ BOS signal created:" -ForegroundColor Green
    Write-Host "  Signal ID: $($bosResponse.signalId)" -ForegroundColor White
    Write-Host "  Active Signal ID: $($bosResponse.activeSignalId)" -ForegroundColor White
    Write-Host "  Symbol: $($bosResponse.symbol)" -ForegroundColor White
    Write-Host "  Price: $($bosResponse.price)" -ForegroundColor White
    Write-Host "  Swing: $($bosResponse.swing)" -ForegroundColor $(if ($bosResponse.swing) { "Green" } else { "Red" })
} catch {
    Write-Host "❌ Failed to create BOS signal: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Step 4: Wait for SignalMonitoringService to process (60 seconds)..." -ForegroundColor Yellow
Write-Host "Service runs every 60 seconds. Waiting for next cycle..." -ForegroundColor Gray

Start-Sleep -Seconds 65

Write-Host ""
Write-Host "Step 5: Check Heroku logs for MetaAPI activity..." -ForegroundColor Yellow
Write-Host ""

$logs = heroku logs -a tradingsignals -n 100 2>&1 | Out-String
$priceLines = $logs -split "`n" | Where-Object { $_ -match "price|MetaApi|XAUUSD|Fetching" }

if ($priceLines) {
    Write-Host "✅ Found MetaAPI activity in logs:" -ForegroundColor Green
    $priceLines | ForEach-Object { Write-Host $_ -ForegroundColor White }
} else {
    Write-Host "⚠️ No MetaAPI activity found in recent logs" -ForegroundColor Yellow
    Write-Host "This could mean:" -ForegroundColor Gray
    Write-Host "  - Service hasn't run yet (wait longer)" -ForegroundColor Gray
    Write-Host "  - No signals need price checking" -ForegroundColor Gray
    Write-Host "  - Check logs manually for details" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Step 6: Check active signals status..." -ForegroundColor Yellow

try {
    $activeSignals = Invoke-RestMethod -Uri "$baseUrl/api/active-signals" -Headers @{"X-API-Key"=$apiKey} -Method GET
    $xauSignals = $activeSignals | Where-Object { $_.symbol -eq "XAUUSD" }
    
    if ($xauSignals) {
        Write-Host "✅ Found $($xauSignals.Count) XAUUSD signals:" -ForegroundColor Green
        foreach ($signal in $xauSignals) {
            Write-Host ""
            Write-Host "  Type: $($signal.type)" -ForegroundColor White
            Write-Host "  Action: $($signal.action)" -ForegroundColor White
            Write-Host "  Price: $($signal.price)" -ForegroundColor White
            Write-Host "  Swing: $($signal.swing)" -ForegroundColor $(if ($signal.swing) { "Green" } else { "Gray" })
            Write-Host "  Resolved: $($signal.resolved)" -ForegroundColor $(if ($signal.resolved) { "Yellow" } else { "White" })
            Write-Host "  Used: $($signal.used)" -ForegroundColor $(if ($signal.used) { "Yellow" } else { "White" })
        }
    } else {
        Write-Host "⚠️ No XAUUSD signals found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Failed to get active signals: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Test Complete!" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
