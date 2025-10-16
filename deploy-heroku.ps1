# Quick deploy script cho Heroku

param(
    [string]$AppName = "",
    [string]$CommitMessage = "Update application"
)

# Colors
function Write-Success { param($message) Write-Host $message -ForegroundColor Green }
function Write-Info { param($message) Write-Host $message -ForegroundColor Cyan }
function Write-Error { param($message) Write-Host $message -ForegroundColor Red }

Write-Info "=== Heroku Deployment Script ==="
Write-Host ""

# Check if Heroku CLI is installed
try {
    $herokuVersion = heroku --version
    Write-Success "✓ Heroku CLI detected: $($herokuVersion.Split([Environment]::NewLine)[0])"
}
catch {
    Write-Error "✗ Heroku CLI not found. Please install it from https://devcenter.heroku.com/articles/heroku-cli"
    exit 1
}

# Check if git is initialized
if (-not (Test-Path ".git")) {
    Write-Info "Initializing git repository..."
    git init
    git add .
    git commit -m "Initial commit"
    Write-Success "✓ Git repository initialized"
}

# Get app name if not provided
if ([string]::IsNullOrEmpty($AppName)) {
    Write-Host ""
    $AppName = Read-Host "Enter your Heroku app name"
}

# Check if app exists
try {
    heroku apps:info -a $AppName | Out-Null
    Write-Success "✓ Found Heroku app: $AppName"
    $isNewApp = $false
}
catch {
    Write-Info "App not found. Creating new app: $AppName"
    try {
        heroku create $AppName
        Write-Success "✓ Created new Heroku app: $AppName"
        $isNewApp = $true
    }
    catch {
        Write-Error "✗ Failed to create app. It may already exist or the name is taken."
        exit 1
    }
}

# Add PostgreSQL if new app
if ($isNewApp) {
    Write-Info "Adding PostgreSQL addon..."
    try {
        heroku addons:create heroku-postgresql:mini -a $AppName
        Write-Success "✓ PostgreSQL addon added"
    }
    catch {
        Write-Error "✗ Failed to add PostgreSQL addon: $($_.Exception.Message)"
    }
    
    # Set environment variables
    Write-Info "Setting environment variables..."
    heroku config:set API_KEY=kyuoj1KRGILRy4Le9i8NtXGDdFIspy07 -a $AppName
    heroku config:set CONFIG_API_KEY=uHJuLHD70Ju6N97mkQcmWzVTBUxsnscI -a $AppName
    heroku config:set ASPNETCORE_ENVIRONMENT=Production -a $AppName
    Write-Success "✓ Environment variables set"
}

# Set buildpack
Write-Info "Configuring buildpack..."
try {
    heroku buildpacks:set https://github.com/jincod/dotnetcore-buildpack -a $AppName
    Write-Success "✓ Buildpack configured"
}
catch {
    Write-Info "  (Buildpack may already be set)"
}

# Add Heroku remote if not exists
$remotes = git remote -v 2>$null
if ($remotes -notmatch "heroku") {
    Write-Info "Adding Heroku remote..."
    heroku git:remote -a $AppName
    Write-Success "✓ Heroku remote added"
}

# Commit changes
Write-Host ""
Write-Info "Committing changes..."
git add .
git commit -m $CommitMessage -q 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Success "✓ Changes committed"
} else {
    Write-Info "  (No changes to commit)"
}

# Deploy to Heroku
Write-Host ""
Write-Info "Deploying to Heroku..."
Write-Info "This may take a few minutes..."
Write-Host ""

try {
    git push heroku main
    Write-Host ""
    Write-Success "✓ Deployment successful!"
}
catch {
    # Try master branch if main doesn't work
    Write-Info "Trying master branch..."
    git push heroku master
    Write-Host ""
    Write-Success "✓ Deployment successful!"
}

# Show app info
Write-Host ""
Write-Info "=== Deployment Complete ==="
Write-Host ""
Write-Info "App URL: https://$AppName.herokuapp.com"
Write-Info "Swagger UI: https://$AppName.herokuapp.com/swagger"
Write-Host ""

# Ask if user wants to open the app
$open = Read-Host "Open app in browser? (y/n)"
if ($open -eq "y") {
    heroku open -a $AppName
}

# Ask if user wants to see logs
$logs = Read-Host "View logs? (y/n)"
if ($logs -eq "y") {
    heroku logs --tail -a $AppName
}

Write-Host ""
Write-Success "Done! 🎉"
