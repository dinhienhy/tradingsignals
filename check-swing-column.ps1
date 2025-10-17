# Quick check if Swing is being saved
$baseUrl = "https://tradingsignals-ae14b4a15912.herokuapp.com"
$apiKey = "LaxArXqne4NxVstvl9PdDNvj2OJ00vvX"

Write-Host "Fetching Active Signals to check Swing column..." -ForegroundColor Cyan
Write-Host ""

try {
    $signals = Invoke-RestMethod -Uri "$baseUrl/api/active-signals" -Headers @{"X-API-Key"=$apiKey} -Method GET
    
    Write-Host "Found $($signals.Count) active signals" -ForegroundColor Green
    Write-Host ""
    
    foreach ($signal in $signals) {
        Write-Host "Signal ID: $($signal.id)" -ForegroundColor Yellow
        Write-Host "  Symbol: $($signal.symbol)" -ForegroundColor White
        Write-Host "  Type: $($signal.type)" -ForegroundColor White
        Write-Host "  Action: $($signal.action)" -ForegroundColor White
        Write-Host "  Price: $($signal.price)" -ForegroundColor White
        Write-Host "  Swing: $($signal.swing)" -ForegroundColor $(if ($signal.swing) { "Green" } else { "Red" })
        Write-Host "  Timestamp: $($signal.timestamp)" -ForegroundColor White
        Write-Host "  Has 'swing' property: $(if ($signal.PSObject.Properties.Name -contains 'swing') { 'YES' } else { 'NO' })" -ForegroundColor Cyan
        Write-Host ""
    }
    
    $withSwing = $signals | Where-Object { $_.swing -ne $null }
    Write-Host "Signals with Swing: $($withSwing.Count) / $($signals.Count)" -ForegroundColor $(if ($withSwing.Count -gt 0) { "Green" } else { "Red" })
    
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Host $reader.ReadToEnd() -ForegroundColor Yellow
    }
}
