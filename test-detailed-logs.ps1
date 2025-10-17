# Test script to create test signals and see detailed logs
param(
    [string]$BaseUrl = "https://tradingsignals-ae14b4a15912.herokuapp.com",
    [string]$WebhookSecret = "LaxArXqne4NxVstvl9PdDNvj2OJ00vvX"
)

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Testing Detailed Logs System" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# 1. Send BOS signal (with Swing)
Write-Host "1. Sending EntryBOS signal..." -ForegroundColor Yellow
$bosPayload = @{
    symbol = "EURUSD"
    action = "BUY"
    price = 1.05000
    swing = 1.04800
} | ConvertTo-Json

$headers = @{
    "Content-Type" = "application/json"
    "X-Webhook-Secret" = $WebhookSecret
}

try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/webhook/EntryBOS" -Method POST -Body $bosPayload -Headers $headers -UseBasicParsing
    Write-Host "✅ BOS signal sent successfully" -ForegroundColor Green
    Write-Host "Response: $($response.StatusCode)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Failed to send BOS: $_" -ForegroundColor Red
}

Start-Sleep -Seconds 2

# 2. Send CHoCH signal
Write-Host ""
Write-Host "2. Sending EntryCHoCH signal..." -ForegroundColor Yellow
$chochPayload = @{
    symbol = "EURUSD"
    action = "BUY"
    price = 1.05200
} | ConvertTo-Json

try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/webhook/EntryCHoCH" -Method POST -Body $chochPayload -Headers $headers -UseBasicParsing
    Write-Host "✅ CHoCH signal sent successfully" -ForegroundColor Green
    Write-Host "Response: $($response.StatusCode)" -ForegroundColor Gray
} catch {
    Write-Host "❌ Failed to send CHoCH: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Test signals created!" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "📊 View detailed logs at:" -ForegroundColor Cyan
Write-Host "$BaseUrl/logs.html" -ForegroundColor White
Write-Host ""
Write-Host "🔍 Service will process these signals in the next cycle (within 1 minute)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Expected logs to see:" -ForegroundColor Cyan
Write-Host "- ProcessStart: Starting to process signals" -ForegroundColor Gray
Write-Host "- ProcessSymbol: Processing EURUSD signals" -ForegroundColor Gray
Write-Host "- FetchPrice: Price fetched from MetaApi" -ForegroundColor Gray
Write-Host "- QueryBOS: Found BOS signals" -ForegroundColor Gray
Write-Host "- CheckPrice: Comparing price vs BOS Swing" -ForegroundColor Gray
Write-Host "- PriceNotBroken or PriceBreakResolved: Decision result" -ForegroundColor Gray
Write-Host ""
