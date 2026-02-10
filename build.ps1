#!/usr/bin/env pwsh
# Build script for MeirDownloader

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "./publish"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " MeirDownloader Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Restore
Write-Host "[1/3] Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore MeirDownloader.sln
if ($LASTEXITCODE -ne 0) { Write-Host "Restore failed!" -ForegroundColor Red; exit 1 }
Write-Host "Restore completed." -ForegroundColor Green
Write-Host ""

# Step 2: Build
Write-Host "[2/3] Building solution ($Configuration)..." -ForegroundColor Yellow
dotnet build MeirDownloader.sln --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { Write-Host "Build failed!" -ForegroundColor Red; exit 1 }
Write-Host "Build completed." -ForegroundColor Green
Write-Host ""

# Step 3: Publish API
Write-Host "[3/3] Publishing API to $OutputDir..." -ForegroundColor Yellow
if (Test-Path $OutputDir) { Remove-Item -Recurse -Force $OutputDir }
dotnet publish MeirDownloader.Api/MeirDownloader.Api.csproj --configuration $Configuration --output $OutputDir --no-build
if ($LASTEXITCODE -ne 0) { Write-Host "Publish failed!" -ForegroundColor Red; exit 1 }
Write-Host "Publish completed." -ForegroundColor Green
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Build Successful!" -ForegroundColor Green
Write-Host " Published to: $OutputDir" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
