# PowerShell script to test the Trading Signals API
$baseUrl = "http://localhost:5124"
$configApiKey = "your-config-api-key-here" # Replace with your actual config API key
$apiKey = "your-api-key-here" # Replace with your actual API key

# Helper function to display responses
function Show-Response {
    param (
        [string]$title,
        [object]$response
    )
    Write-Host "`n--- $title ---" -ForegroundColor Green
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Cyan
    if ($response.Content) {
        try {
            $content = $response.Content | ConvertFrom-Json
            $content | ConvertTo-Json -Depth 5
        } catch {
            $response.Content
        }
    }
}

# Step 1: Create a webhook configuration
$webhookConfig = @{
    path = "test-webhook"
    secret = "test-secret"
    description = "Test webhook for trading signals"
} | ConvertTo-Json

Write-Host "Creating webhook configuration..." -ForegroundColor Yellow
$createWebhookResponse = Invoke-RestMethod -Uri "$baseUrl/config/webhooks" -Method Post -Body $webhookConfig -ContentType "application/json" -Headers @{ "ConfigApiKey" = $configApiKey } -ErrorAction SilentlyContinue -ErrorVariable createWebhookError

if ($createWebhookError) {
    Show-Response "Create Webhook Error" $createWebhookError.ErrorRecord.Exception.Response
} else {
    Show-Response "Create Webhook Response" $createWebhookResponse
    $webhookId = $createWebhookResponse.id
}

# Step 2: Get all webhook configurations
Write-Host "Getting all webhook configurations..." -ForegroundColor Yellow
$getWebhooksResponse = Invoke-RestMethod -Uri "$baseUrl/config/webhooks" -Method Get -Headers @{ "ConfigApiKey" = $configApiKey } -ErrorAction SilentlyContinue -ErrorVariable getWebhooksError

if ($getWebhooksError) {
    Show-Response "Get Webhooks Error" $getWebhooksError.ErrorRecord.Exception.Response
} else {
    Show-Response "Get Webhooks Response" $getWebhooksResponse
}

# Step 3: Send a trading signal via webhook
$tradingSignal = @{
    secret = "test-secret"
    symbol = "EURUSD"
    action = "BUY"
    price = 1.05678
    timestamp = (Get-Date).ToString("o")
    message = "Test trading signal from PowerShell"
} | ConvertTo-Json

Write-Host "Sending trading signal..." -ForegroundColor Yellow
$sendSignalResponse = Invoke-RestMethod -Uri "$baseUrl/webhook/test-webhook" -Method Post -Body $tradingSignal -ContentType "application/json" -ErrorAction SilentlyContinue -ErrorVariable sendSignalError

if ($sendSignalError) {
    Show-Response "Send Signal Error" $sendSignalError.ErrorRecord.Exception.Response
} else {
    Show-Response "Send Signal Response" $sendSignalResponse
}

# Step 4: Get pending signals
Write-Host "Getting pending signals..." -ForegroundColor Yellow
$getPendingSignalsResponse = Invoke-RestMethod -Uri "$baseUrl/signals/pending" -Method Get -Headers @{ "ApiKey" = $apiKey } -ErrorAction SilentlyContinue -ErrorVariable getPendingSignalsError

if ($getPendingSignalsError) {
    Show-Response "Get Pending Signals Error" $getPendingSignalsError.ErrorRecord.Exception.Response
} else {
    Show-Response "Get Pending Signals Response" $getPendingSignalsResponse
}

# Step 5: Delete the webhook configuration if it was created
if ($webhookId) {
    Write-Host "Deleting webhook configuration..." -ForegroundColor Yellow
    $deleteWebhookResponse = Invoke-RestMethod -Uri "$baseUrl/config/webhooks/$webhookId" -Method Delete -Headers @{ "ConfigApiKey" = $configApiKey } -ErrorAction SilentlyContinue -ErrorVariable deleteWebhookError

    if ($deleteWebhookError) {
        Show-Response "Delete Webhook Error" $deleteWebhookError.ErrorRecord.Exception.Response
    } else {
        Write-Host "Webhook deleted successfully" -ForegroundColor Green
    }
}

Write-Host "`nAPI testing completed!" -ForegroundColor Green
