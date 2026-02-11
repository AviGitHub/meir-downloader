#!/usr/bin/env pwsh
# Build script for MeirDownloader Installer
# This script publishes the Desktop app and builds the WiX MSI installer

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$PublishDir = "./publish-desktop"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " MeirDownloader Installer Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean previous publish output
Write-Host "[1/4] Cleaning previous publish output..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
    Write-Host "  Cleaned $PublishDir" -ForegroundColor Gray
}
Write-Host "Clean completed." -ForegroundColor Green
Write-Host ""

# Step 2: Restore
Write-Host "[2/4] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore MeirDownloader.sln
if ($LASTEXITCODE -ne 0) { Write-Host "Restore failed!" -ForegroundColor Red; exit 1 }
Write-Host "Restore completed." -ForegroundColor Green
Write-Host ""

# Step 3: Publish Desktop app as self-contained
Write-Host "[3/4] Publishing Desktop app (self-contained, $Runtime)..." -ForegroundColor Yellow
dotnet publish MeirDownloader.Desktop/MeirDownloader.Desktop.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained `
    -o $PublishDir
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed!" -ForegroundColor Red; exit 1 }
Write-Host "Publish completed to $PublishDir" -ForegroundColor Green
Write-Host ""

# Step 4: Build WiX installer
Write-Host "[4/4] Building WiX installer..." -ForegroundColor Yellow
dotnet build MeirDownloader.Installer/MeirDownloader.Installer.wixproj -c $Configuration
if ($LASTEXITCODE -ne 0) { Write-Host "Installer build failed!" -ForegroundColor Red; exit 1 }
Write-Host "Installer build completed." -ForegroundColor Green
Write-Host ""

# Find the MSI output
$msiPath = Get-ChildItem -Path "MeirDownloader.Installer/bin/$Configuration" -Filter "*.msi" -Recurse | Select-Object -First 1
if ($msiPath) {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " Installer Build Successful!" -ForegroundColor Green
    Write-Host " MSI: $($msiPath.FullName)" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
} else {
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " Build completed. Check MeirDownloader.Installer/bin/$Configuration for output." -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Cyan
}
