# Test webhook with Swing parameter
$baseUrl = "https://tradingsignals-ae14b4a15912.herokuapp.com"

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Testing Webhook Swing Parameter" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Create webhook config first (if needed)
Write-Host "Step 1: Check/Create webhook config..." -ForegroundColor Yellow
$configApiKey = "LaxArXqne4NxVstvl9PdDNvj2OJ00vvX"

# Try to get existing webhooks
try {
    $webhooks = Invoke-RestMethod -Uri "$baseUrl/api/webhooks" -Method GET -Headers @{"X-API-Key"=$configApiKey}
    Write-Host "Existing webhooks:" -ForegroundColor Green
    $webhooks | ForEach-Object { Write-Host "  - $($_.path): $($_.description)" -ForegroundColor Gray }
} catch {
    Write-Host "Could not fetch webhooks: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Step 2: Send test signal with Swing..." -ForegroundColor Yellow

# Prepare payload with Swing
$payload = @{
    secret = "test-webhook-secret-123"
    symbol = "EURUSD"
    action = "BUY"
    price = 1.05000
    swing = 1.04800
    timestamp = (Get-Date).ToUniversalTime().ToString("o")
} | ConvertTo-Json

Write-Host "Payload:" -ForegroundColor Gray
Write-Host $payload -ForegroundColor Gray
Write-Host ""

# Send to webhook
try {
    $response = Invoke-RestMethod -Uri "$baseUrl/webhook/TestSwing" -Method POST -Body $payload -ContentType "application/json"
    Write-Host "✅ Webhook Response:" -ForegroundColor Green
    $response | ConvertTo-Json -Depth 5 | Write-Host -ForegroundColor White
    Write-Host ""
    
    if ($response.swing) {
        Write-Host "✅ Swing received: $($response.swing)" -ForegroundColor Green
    } else {
        Write-Host "❌ Swing NOT in response!" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Webhook failed:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseText = $reader.ReadToEnd()
        Write-Host "Response: $responseText" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Step 3: Check Active Signals..." -ForegroundColor Yellow
try {
    $activeSignals = Invoke-RestMethod -Uri "$baseUrl/api/active-signals" -Method GET -Headers @{"X-API-Key"=$configApiKey}
    
    $testSignal = $activeSignals | Where-Object { $_.type -eq "TestSwing" -and $_.symbol -eq "EURUSD" } | Select-Object -First 1
    
    if ($testSignal) {
        Write-Host "✅ Found active signal:" -ForegroundColor Green
        Write-Host "  Symbol: $($testSignal.symbol)" -ForegroundColor White
        Write-Host "  Action: $($testSignal.action)" -ForegroundColor White
        Write-Host "  Price: $($testSignal.price)" -ForegroundColor White
        Write-Host "  Swing: $($testSignal.swing)" -ForegroundColor $(if ($testSignal.swing) { "Green" } else { "Red" })
        Write-Host "  Timestamp: $($testSignal.timestamp)" -ForegroundColor White
        
        if ($testSignal.swing) {
            Write-Host ""
            Write-Host "✅✅✅ SWING PARAMETER WORKING! ✅✅✅" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "❌❌❌ SWING NOT SAVED TO DATABASE! ❌❌❌" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ Test signal not found in active signals" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Could not fetch active signals: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
