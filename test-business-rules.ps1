# Test Business Rules Engine
# Usage: .\test-business-rules.ps1 -BaseUrl "https://tradingsignals.herokuapp.com" -ApiKey "your-api-key"

param(
    [Parameter(Mandatory=$false)]
    [string]$BaseUrl = "http://localhost:5000",
    
    [Parameter(Mandatory=$true)]
    [string]$ApiKey
)

$headers = @{
    "Content-Type" = "application/json"
    "X-API-Key" = $ApiKey
}

function Write-TestResult {
    param($TestName, $Success, $Message)
    
    if ($Success) {
        Write-Host "✓ $TestName" -ForegroundColor Green
    } else {
        Write-Host "✗ $TestName" -ForegroundColor Red
    }
    if ($Message) {
        Write-Host "  $Message" -ForegroundColor Gray
    }
}

Write-Host "`n=== Testing Business Rules Engine ===" -ForegroundColor Cyan
Write-Host "Base URL: $BaseUrl`n"

# Test 1: Valid Signal
Write-Host "[Test 1] Valid Signal - Should Pass All Rules" -ForegroundColor Yellow
$validSignal = @{
    symbol = "EURUSD"
    action = "BUY"
    price = 1.05432
    type = "scalping"
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/activesignals/validate" `
        -Method Post `
        -Headers $headers `
        -Body $validSignal `
        -ErrorAction Stop
    
    Write-TestResult "Valid Signal" $response.success $response.message
    Write-Host "  Validation Details:" -ForegroundColor Gray
    $response.validationDetails.ruleResults.PSObject.Properties | ForEach-Object {
        $ruleName = $_.Name
        $ruleResult = $_.Value
        Write-Host "    - $ruleName`: " -NoNewline -ForegroundColor Gray
        if ($ruleResult.isValid) {
            Write-Host "PASS" -ForegroundColor Green
        } else {
            Write-Host "FAIL - $($ruleResult.message)" -ForegroundColor Red
        }
    }
}
catch {
    Write-TestResult "Valid Signal" $false "Error: $($_.Exception.Message)"
}

Start-Sleep -Seconds 2

# Test 2: Invalid Price
Write-Host "`n[Test 2] Invalid Price - Should Fail PriceValidationRule" -ForegroundColor Yellow
$invalidPriceSignal = @{
    symbol = "EURUSD"
    action = "BUY"
    price = -1.0
    type = "scalping"
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/activesignals/validate" `
        -Method Post `
        -Headers $headers `
        -Body $invalidPriceSignal `
        -ErrorAction Stop
    
    Write-TestResult "Invalid Price Detection" (!$response.success) $response.message
}
catch {
    Write-TestResult "Invalid Price Detection" $false "Error: $($_.Exception.Message)"
}

Start-Sleep -Seconds 2

# Test 3: Duplicate Signal
Write-Host "`n[Test 3] Duplicate Signal - Should Fail DuplicateSignalRule" -ForegroundColor Yellow
$signal1 = @{
    symbol = "GBPUSD"
    action = "SELL"
    price = 1.25000
    type = "swing"
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

# Send first signal
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/activesignals/validate" `
        -Method Post `
        -Headers $headers `
        -Body $signal1 `
        -ErrorAction Stop | Out-Null
    
    Write-Host "  First signal sent" -ForegroundColor Gray
    
    Start-Sleep -Seconds 1
    
    # Send duplicate
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/activesignals/validate" `
        -Method Post `
        -Headers $headers `
        -Body $signal1 `
        -ErrorAction Stop
    
    Write-TestResult "Duplicate Detection" (!$response.success) $response.message
}
catch {
    Write-TestResult "Duplicate Detection" $false "Error: $($_.Exception.Message)"
}

Start-Sleep -Seconds 2

# Test 4: Swing Detection
Write-Host "`n[Test 4] Swing Detection - Should Auto-Set Swing" -ForegroundColor Yellow
$signalWithoutSwing = @{
    symbol = "USDJPY"
    action = "BUY"
    price = 150.250
    type = "trend"
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/activesignals/validate" `
        -Method Post `
        -Headers $headers `
        -Body $signalWithoutSwing `
        -ErrorAction Stop
    
    $swingSet = ![string]::IsNullOrEmpty($response.processedSignal.swing)
    Write-TestResult "Swing Auto-Detection" $swingSet "Swing: $($response.processedSignal.swing)"
}
catch {
    Write-TestResult "Swing Auto-Detection" $false "Error: $($_.Exception.Message)"
}

Start-Sleep -Seconds 2

# Test 5: EntryCHoCH Signal
Write-Host "`n[Test 5] EntryCHoCH Signal - Change of Character" -ForegroundColor Yellow
$chochSignal = @{
    symbol = "EURUSD"
    action = "BUY"
    price = 1.05500
    type = "EntryCHoCH"
    swing = "1.05000"
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/activesignals/validate" `
        -Method Post `
        -Headers $headers `
        -Body $chochSignal `
        -ErrorAction Stop
    
    Write-TestResult "EntryCHoCH Validation" $response.success $response.message
    if ($response.validationDetails.ruleResults.EntryCHoCHRule) {
        Write-Host "  EntryCHoCH Rule: " -NoNewline -ForegroundColor Gray
        if ($response.validationDetails.ruleResults.EntryCHoCHRule.isValid) {
            Write-Host "PASS" -ForegroundColor Green
        } else {
            Write-Host "FAIL - $($response.validationDetails.ruleResults.EntryCHoCHRule.message)" -ForegroundColor Red
        }
    }
}
catch {
    Write-TestResult "EntryCHoCH Validation" $false "Error: $($_.Exception.Message)"
}

Start-Sleep -Seconds 2

# Test 6: EntryBOS Signal
Write-Host "`n[Test 6] EntryBOS Signal - Break of Structure" -ForegroundColor Yellow
$bosSignal = @{
    symbol = "GBPUSD"
    action = "SELL"
    price = 1.24500
    type = "EntryBOS"
    swing = "1.25000"
    timestamp = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
} | ConvertTo-Json

try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/activesignals/validate" `
        -Method Post `
        -Headers $headers `
        -Body $bosSignal `
        -ErrorAction Stop
    
    Write-TestResult "EntryBOS Validation" $response.success $response.message
    if ($response.validationDetails.ruleResults.EntryBOSRule) {
        Write-Host "  EntryBOS Rule: " -NoNewline -ForegroundColor Gray
        if ($response.validationDetails.ruleResults.EntryBOSRule.isValid) {
            Write-Host "PASS" -ForegroundColor Green
        } else {
            Write-Host "FAIL - $($response.validationDetails.ruleResults.EntryBOSRule.message)" -ForegroundColor Red
        }
    }
}
catch {
    Write-TestResult "EntryBOS Validation" $false "Error: $($_.Exception.Message)"
}

Start-Sleep -Seconds 2

# Test 7: Run Maintenance
Write-Host "`n[Test 7] Maintenance Task" -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$BaseUrl/api/activesignals/maintenance" `
        -Method Post `
        -Headers $headers `
        -ErrorAction Stop
    
    Write-TestResult "Maintenance Execution" $true $response.message
}
catch {
    Write-TestResult "Maintenance Execution" $false "Error: $($_.Exception.Message)"
}

# Summary
Write-Host "`n=== Test Summary ===" -ForegroundColor Cyan
Write-Host "All tests completed. Check results above.`n"

# Example: Add custom rule
Write-Host "=== Example: Add Custom Rule ===" -ForegroundColor Cyan
Write-Host @"
1. Create new rule class in BusinessRules/Rules/
2. Implement ISignalRule interface
3. Register in Program.cs:
   builder.Services.AddTransient<ISignalRule, YourCustomRule>();
4. Deploy and test!
"@ -ForegroundColor Gray

Write-Host ""
