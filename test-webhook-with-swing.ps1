# Script to test webhook with Swing field

# Configuration
$baseUrl = "http://localhost:5000"
$webhookPath = "test"  # Change this to your webhook path
$webhookSecret = "test-secret"  # Change this to your webhook secret
$apiKey = "your-api-key-here"  # Change this to your API key

# Colors for output
function Write-Success { param($message) Write-Host $message -ForegroundColor Green }
function Write-Info { param($message) Write-Host $message -ForegroundColor Cyan }
function Write-Error { param($message) Write-Host $message -ForegroundColor Red }

Write-Info "=== Testing Webhook with Swing Field ==="
Write-Host ""

# Test 1: Create webhook config (if not exists)
Write-Info "Step 1: Creating webhook configuration..."
try {
    $webhookConfig = @{
        path = $webhookPath
        secret = $webhookSecret
        description = "Test webhook for swing feature"
    } | ConvertTo-Json
    
    $configResponse = Invoke-RestMethod -Uri "$baseUrl/config/webhooks" `
        -Method Post `
        -Headers @{ "ConfigApiKey" = $apiKey; "Content-Type" = "application/json" } `
        -Body $webhookConfig `
        -ErrorAction SilentlyContinue
    
    Write-Success "✓ Webhook config created"
}
catch {
    Write-Info "  (Webhook config may already exist)"
}

# Test 2: Send signal with Swing field
Write-Host ""
Write-Info "Step 2: Sending trading signal with Swing..."

$signalData = @{
    secret = $webhookSecret
    symbol = "EURUSD"
    action = "BUY"
    price = 1.0545
    swing = 1.0500
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    message = "Test signal with swing level"
} | ConvertTo-Json

try {
    $webhookResponse = Invoke-RestMethod -Uri "$baseUrl/webhook/$webhookPath" `
        -Method Post `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body $signalData
    
    Write-Success "✓ Signal sent successfully"
    Write-Host "  Signal ID: $($webhookResponse.signalId)"
}
catch {
    Write-Error "✗ Failed to send signal: $($_.Exception.Message)"
    exit 1
}

# Test 3: Retrieve active signals
Write-Host ""
Write-Info "Step 3: Retrieving active signals..."

try {
    $activeSignals = Invoke-RestMethod -Uri "$baseUrl/api/activesignals" `
        -Method Get `
        -Headers @{ "X-API-Key" = $apiKey }
    
    Write-Success "✓ Retrieved $($activeSignals.Count) active signal(s)"
    
    # Display signal details
    foreach ($signal in $activeSignals) {
        Write-Host ""
        Write-Info "=== Signal Details ==="
        Write-Host "ID:         $($signal.id)"
        Write-Host "Symbol:     $($signal.symbol)"
        Write-Host "Action:     $($signal.action)"
        Write-Host "Price:      $($signal.price)"
        Write-Host "Swing:      $($signal.swing)"
        Write-Host "Type:       $($signal.type)"
        Write-Host "Used:       $($signal.used)"
        Write-Host "Resolved:   $($signal.resolved)"
        Write-Host "Timestamp:  $($signal.timestamp)"
    }
}
catch {
    Write-Error "✗ Failed to retrieve signals: $($_.Exception.Message)"
}

# Test 4: Send signal without Swing (optional field)
Write-Host ""
Write-Info "Step 4: Sending signal WITHOUT Swing field..."

$signalData2 = @{
    secret = $webhookSecret
    symbol = "GBPUSD"
    action = "SELL"
    price = 1.2345
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

try {
    $webhookResponse2 = Invoke-RestMethod -Uri "$baseUrl/webhook/$webhookPath" `
        -Method Post `
        -Headers @{ "Content-Type" = "application/json" } `
        -Body $signalData2
    
    Write-Success "✓ Signal without Swing sent successfully"
}
catch {
    Write-Error "✗ Failed to send signal: $($_.Exception.Message)"
}

Write-Host ""
Write-Info "=== Tests Completed ==="
Write-Host ""
Write-Info "Notes:"
Write-Host "  - Field 'swing' is optional in webhook payload"
Write-Host "  - If 'swing' is not provided, it will be stored as NULL"
Write-Host "  - When new signal arrives, 'used' and 'resolved' flags are reset to false"
