# Test Signal Monitoring Background Service
# Usage: .\test-monitoring-service.ps1 -BaseUrl "https://tradingsignals.herokuapp.com" -WebhookSecret "your-secret"

param(
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "http://localhost:5000",
    
    [Parameter(Mandatory=$true)]
    [string]$WebhookSecret
)

function Write-TestHeader {
    param($Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Send-TestSignal {
    param(
        [string]$Type,
        [string]$Symbol,
        [string]$Action,
        [decimal]$Price,
        [string]$Swing = $null
    )
    
    $payload = @{
        secret = $WebhookSecret
        symbol = $Symbol
        action = $Action
        price = $Price
    }
    
    if ($Swing) {
        $payload.swing = $Swing
    }
    
    $body = $payload | ConvertTo-Json
    
    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/webhook/$Type" `
            -Method Post `
            -ContentType "application/json" `
            -Body $body `
            -ErrorAction Stop
        
        Write-Host "✓ Signal sent: $Type $Symbol $Action @ $Price" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "✗ Failed to send signal: $($_.Exception.Message)" -ForegroundColor Red
        return $null
    }
}

Write-TestHeader "Signal Monitoring Service Test"

Write-Host "Base URL: $BaseUrl"
Write-Host "Testing monitoring service behavior...`n"

# Test 1: Create CHoCH signals to test reversal detection
Write-TestHeader "Test 1: CHoCH Reversal Detection"
Write-Host "Sending CHoCH SELL signal..."
Send-TestSignal -Type "EntryCHoCH" -Symbol "EURUSD" -Action "SELL" -Price 1.06000

Start-Sleep -Seconds 2

Write-Host "Sending opposite CHoCH BUY signal (should trigger auto-resolve)..."
Send-TestSignal -Type "EntryCHoCH" -Symbol "EURUSD" -Action "BUY" -Price 1.05500

Write-Host "`n⏱️  Wait 1-2 minutes for background service to process..."
Write-Host "Expected: Old SELL signal should be auto-resolved"

# Test 2: Create BOS with conflicting CHoCH
Write-TestHeader "Test 2: BOS Conflict Detection"
Write-Host "Sending BOS BUY signal..."
Send-TestSignal -Type "EntryBOS" -Symbol "GBPUSD" -Action "BUY" -Price 1.25500 -Swing "1.25000"

Start-Sleep -Seconds 2

Write-Host "Sending conflicting CHoCH SELL signal..."
Send-TestSignal -Type "EntryCHoCH" -Symbol "GBPUSD" -Action "SELL" -Price 1.25300

Write-Host "`n⏱️  Wait 1-2 minutes for background service to process..."
Write-Host "Expected: BOS BUY should be auto-resolved due to opposite CHoCH"

# Test 3: Multiple same-direction signals
Write-TestHeader "Test 3: Multiple Same-Direction Signals"
Write-Host "Sending 3 BOS SELL signals for USDJPY..."

Send-TestSignal -Type "EntryBOS" -Symbol "USDJPY" -Action "SELL" -Price 150.500
Start-Sleep -Seconds 1
Send-TestSignal -Type "EntryBOS" -Symbol "USDJPY" -Action "SELL" -Price 150.450
Start-Sleep -Seconds 1
Send-TestSignal -Type "EntryBOS" -Symbol "USDJPY" -Action "SELL" -Price 150.400

Write-Host "`n⏱️  All signals should remain active (same direction = trend confirmation)"

# Test 4: Generic signal
Write-TestHeader "Test 4: Generic Signal Type"
Write-Host "Sending generic signal (scalping)..."
Send-TestSignal -Type "scalping" -Symbol "XAUUSD" -Action "BUY" -Price 2050.50

Write-Host "`n⏱️  This signal will auto-resolve after 24 hours"

# Instructions
Write-TestHeader "How to Monitor"

Write-Host @"
1️⃣  Check Heroku Logs (Real-time):
   heroku logs --tail -a tradingsignals

2️⃣  Filter for monitoring service:
   heroku logs --tail -a tradingsignals | grep "SignalMonitoring"

3️⃣  Filter for specific signal types:
   heroku logs --tail -a tradingsignals | grep "CHoCH"
   heroku logs --tail -a tradingsignals | grep "BOS"

4️⃣  Check processing results:
   heroku logs --tail -a tradingsignals | grep "Processing complete"

5️⃣  Expected log messages:
   - "Signal Monitoring Service started"
   - "Starting signal processing cycle..."
   - "Found X active unresolved signals"
   - "Auto-resolved old CHoCH: EURUSD SELL"
   - "Resolved CHoCH due to reversal: EURUSD SELL"
   - "Resolved BOS due to opposite CHoCH: GBPUSD BUY"
   - "Processing complete. Resolved: X, Updated: Y"
"@ -ForegroundColor Gray

Write-TestHeader "Timeline"

Write-Host @"
⏱️  Immediate:
   - Signals saved to database
   - Visible in Active Signals tab

⏱️  After 1 minute:
   - Background service processes signals
   - Auto-resolve based on rules
   - Check logs for results

⏱️  After 4 hours:
   - CHoCH signals auto-expire

⏱️  After 8 hours:
   - BOS signals auto-expire

⏱️  After 24 hours:
   - Generic signals auto-expire
"@ -ForegroundColor Yellow

Write-TestHeader "Next Steps"

Write-Host @"
1. Wait 1-2 minutes for background service to run
2. Check Heroku logs for processing messages
3. Refresh Active Signals page to see changes
4. Resolved signals should have Resolved = Yes

If service is not running:
- Check: heroku logs --tail -a tradingsignals | grep "Service started"
- Should see: "Signal Monitoring Service started"
"@ -ForegroundColor Cyan

Write-Host "`n✅ Test signals sent successfully!`n" -ForegroundColor Green
