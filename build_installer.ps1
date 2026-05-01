$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  Phantom-OS Packaging & Build Script" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check for .NET SDK
Write-Host "[1/3] Checking .NET SDK..."
try {
    $dotnetVersion = dotnet --version
    Write-Host "Found .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "Error: .NET SDK is not installed or not in PATH." -ForegroundColor Red
    Write-Host "Please install the .NET 8 SDK from https://dotnet.microsoft.com/download" -ForegroundColor Yellow
    exit 1
}

# Step 2: Publish the application (Self-contained)
Write-Host "`n[2/3] Publishing Phantom-OS (Self-Contained x64)..." -ForegroundColor Cyan
Write-Host "This ensures the app runs on any Windows PC without requiring .NET to be pre-installed."
dotnet publish -c Release -r win-x64 --self-contained true
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}
Write-Host "Publish successful." -ForegroundColor Green

# Step 3: Check for Inno Setup compiler
Write-Host "`n[3/3] Checking for Inno Setup Compiler (ISCC)..." -ForegroundColor Cyan
$isccPath = ""

# Common installation paths for Inno Setup
$pathsToTry = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "iscc"
)

foreach ($path in $pathsToTry) {
    if (Test-Path $path) {
        $isccPath = $path
        break
    }
    if (Get-Command $path -ErrorAction SilentlyContinue) {
        $isccPath = $path
        break
    }
}

if ($isccPath -eq "") {
    Write-Host "Inno Setup Compiler not found!" -ForegroundColor Red
    Write-Host "To generate the setup .exe, you need to install Inno Setup." -ForegroundColor Yellow
    Write-Host "You can install it automatically by running the following command as Administrator:" -ForegroundColor White
    Write-Host "  winget install -e --id JRSoftware.InnoSetup" -ForegroundColor Cyan
    Write-Host "`nAfter installing, re-run this script."
    exit 1
}

# Compile the installer
Write-Host "Found ISCC at: $isccPath" -ForegroundColor Green
Write-Host "Compiling installer executable..."
& $isccPath "installer.iss"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Installer compilation failed!" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "  SUCCESS! Installer generated." -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "You can find your installer in the 'Installer' folder:" -ForegroundColor White
Write-Host "  $(Resolve-Path .\Installer\PhantomOS_Setup.exe)" -ForegroundColor Cyan
