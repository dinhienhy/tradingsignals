# Script debug Heroku deployment issues

param(
    [Parameter(Mandatory=$true)]
    [string]$AppName
)

function Write-Success { param($message) Write-Host $message -ForegroundColor Green }
function Write-Info { param($message) Write-Host $message -ForegroundColor Cyan }
function Write-Error { param($message) Write-Host $message -ForegroundColor Red }
function Write-Warning { param($message) Write-Host $message -ForegroundColor Yellow }

Write-Info "=== Heroku Debug Script ==="
Write-Info "App: $AppName"
Write-Host ""

# 1. Check if app exists
Write-Info "1. Checking app status..."
try {
    $appInfo = heroku apps:info -a $AppName 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Success "✓ App exists and is accessible"
    } else {
        Write-Error "✗ Cannot access app"
        exit 1
    }
}
catch {
    Write-Error "✗ Error checking app: $($_.Exception.Message)"
    exit 1
}

# 2. Check environment variables
Write-Host ""
Write-Info "2. Checking environment variables..."
$config = heroku config -a $AppName 2>&1 | Out-String

if ($config -match "API_KEY") {
    Write-Success "✓ API_KEY is set"
} else {
    Write-Warning "⚠ API_KEY not found"
}

if ($config -match "CONFIG_API_KEY") {
    Write-Success "✓ CONFIG_API_KEY is set"
} else {
    Write-Warning "⚠ CONFIG_API_KEY not found"
}

if ($config -match "DATABASE_URL") {
    Write-Success "✓ DATABASE_URL is set (PostgreSQL connected)"
} else {
    Write-Error "✗ DATABASE_URL not found - PostgreSQL addon may not be installed"
}

if ($config -match "ASPNETCORE_ENVIRONMENT") {
    Write-Success "✓ ASPNETCORE_ENVIRONMENT is set"
} else {
    Write-Warning "⚠ ASPNETCORE_ENVIRONMENT not set"
}

# 3. Check PostgreSQL addon
Write-Host ""
Write-Info "3. Checking PostgreSQL addon..."
try {
    $pgInfo = heroku addons:info postgresql -a $AppName 2>&1 | Out-String
    if ($pgInfo -match "postgresql") {
        Write-Success "✓ PostgreSQL addon is installed"
        Write-Host $pgInfo
    }
}
catch {
    Write-Warning "⚠ Could not get PostgreSQL info"
}

# 4. Check recent logs for errors
Write-Host ""
Write-Info "4. Checking recent logs for errors..."
Write-Host ""
$logs = heroku logs -n 100 -a $AppName 2>&1 | Out-String

# Look for common errors
if ($logs -match "error|exception|fail" -and $logs -notmatch "Failed to load") {
    Write-Warning "⚠ Found errors in logs:"
    Write-Host ""
    $logs -split "`n" | Where-Object { $_ -match "error|exception|fail" } | Select-Object -First 10 | ForEach-Object {
        Write-Host $_ -ForegroundColor Red
    }
} else {
    Write-Success "✓ No obvious errors in recent logs"
}

# Check for migration logs
if ($logs -match "Applying migration") {
    Write-Success "✓ Migrations were applied"
    $logs -split "`n" | Where-Object { $_ -match "Applying migration" } | ForEach-Object {
        Write-Host "  $_" -ForegroundColor Green
    }
} else {
    Write-Warning "⚠ No migration logs found - migrations may not have run"
}

# 5. Test endpoints
Write-Host ""
Write-Info "5. Testing endpoints..."

# Test root
Write-Info "Testing root endpoint..."
try {
    $response = Invoke-WebRequest -Uri "https://$AppName.herokuapp.com/" -Method Get -TimeoutSec 10 -ErrorAction Stop
    Write-Success "✓ Root endpoint responded: $($response.StatusCode)"
}
catch {
    Write-Warning "⚠ Root endpoint failed: $($_.Exception.Message)"
}

# Test Swagger
Write-Info "Testing Swagger UI..."
try {
    $response = Invoke-WebRequest -Uri "https://$AppName.herokuapp.com/swagger" -Method Get -TimeoutSec 10 -ErrorAction Stop
    Write-Success "✓ Swagger UI is accessible: $($response.StatusCode)"
}
catch {
    Write-Warning "⚠ Swagger UI failed: $($_.Exception.Message)"
}

# Test API endpoint
Write-Info "Testing /api/activesignals endpoint..."
try {
    # Extract API key from config
    $apiKey = ($config -split "`n" | Where-Object { $_ -match "API_KEY:" }) -replace ".*API_KEY:\s*", "" | Select-Object -First 1
    $apiKey = $apiKey.Trim()
    
    if ([string]::IsNullOrEmpty($apiKey)) {
        $apiKey = "kyuoj1KRGILRy4Le9i8NtXGDdFIspy07"
    }
    
    $headers = @{ "X-API-Key" = $apiKey }
    $response = Invoke-RestMethod -Uri "https://$AppName.herokuapp.com/api/activesignals" -Method Get -Headers $headers -TimeoutSec 10 -ErrorAction Stop
    Write-Success "✓ Active signals endpoint working"
    Write-Info "  Returned $($response.Count) signals"
}
catch {
    Write-Error "✗ Active signals endpoint failed!"
    if ($_.Exception.Response) {
        Write-Host "  Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    }
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
    
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode.value__ -eq 500) {
        Write-Host ""
        Write-Warning "⚠ 500 Error detected! Common causes:"
        Write-Host "  1. Database migration not applied"
        Write-Host "  2. Database schema mismatch"
        Write-Host "  3. PostgreSQL connection issue"
        Write-Host ""
        Write-Info "Suggested fix:"
        Write-Host "  heroku restart -a $AppName"
        Write-Host "  # Wait 30 seconds then check logs:"
        Write-Host "  heroku logs --tail -a $AppName"
    }
}

# 6. Recommendations
Write-Host ""
Write-Info "=== Recommendations ==="
Write-Host ""

Write-Info "To view live logs:"
Write-Host "  heroku logs --tail -a $AppName" -ForegroundColor Yellow
Write-Host ""

Write-Info "To restart app:"
Write-Host "  heroku restart -a $AppName" -ForegroundColor Yellow
Write-Host ""

Write-Info "To check database:"
Write-Host "  heroku pg:psql -a $AppName" -ForegroundColor Yellow
Write-Host "  Then run: \d `"ActiveTradingSignals`"" -ForegroundColor Yellow
Write-Host ""

Write-Info "To reset database (WARNING: loses all data):"
Write-Host "  heroku pg:reset DATABASE -a $AppName --confirm $AppName" -ForegroundColor Yellow
Write-Host "  heroku restart -a $AppName" -ForegroundColor Yellow
Write-Host ""

Write-Success "Debug complete!"
