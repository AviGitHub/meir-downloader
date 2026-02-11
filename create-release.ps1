#!/usr/bin/env pwsh
# Script to create a GitHub release with the MSI installer
# Prerequisites: GitHub CLI (gh) must be installed and authenticated

param(
    [string]$Version = "1.0.0",
    [string]$MsiPath = "MeirDownloader.Installer/bin/Release/MeirDownloader.Installer.msi"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " MeirDownloader GitHub Release Script" -ForegroundColor Cyan
Write-Host " Version: v$Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    Write-Host "GitHub CLI (gh) is not installed." -ForegroundColor Red
    Write-Host "Install it with: winget install GitHub.cli" -ForegroundColor Yellow
    Write-Host "Then authenticate with: gh auth login" -ForegroundColor Yellow
    exit 1
}

# Check MSI exists
if (-not (Test-Path $MsiPath)) {
    Write-Host "MSI not found at $MsiPath" -ForegroundColor Red
    Write-Host "Run 'powershell -File build-installer.ps1' first." -ForegroundColor Yellow
    exit 1
}

$msiSize = [math]::Round((Get-Item $MsiPath).Length / 1MB, 1)
Write-Host "MSI found: $MsiPath ($msiSize MB)" -ForegroundColor Green
Write-Host ""

# Create git tag
Write-Host "[1/3] Creating git tag v$Version..." -ForegroundColor Yellow
git tag -a "v$Version" -m "Release v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Tag may already exist. Continuing..." -ForegroundColor Yellow
}

# Push tag
Write-Host "[2/3] Pushing tag to GitHub..." -ForegroundColor Yellow
git push origin "v$Version"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to push tag. Make sure remote is configured." -ForegroundColor Red
    exit 1
}

# Create release with MSI
Write-Host "[3/3] Creating GitHub release..." -ForegroundColor Yellow
$releaseNotes = @"
# מוריד שיעורים - Meir Downloader v$Version

## Features
- Download Torah lessons from Machon Meir (meirtv.com)
- Browse rabbis and series with lesson counts
- Parallel downloads (up to 4 simultaneous)
- Per-lesson progress bars
- Skip already-downloaded lessons
- Israeli date format (dd.MM.yyyy)
- Incremental loading with progress indicators
- Self-contained installer (no .NET runtime required)

## Installation
1. Download ``MeirDownloader.Installer.msi`` below
2. Run the installer
3. Launch from Desktop or Start Menu shortcut

## System Requirements
- Windows 10/11 (x64)
- Internet connection
"@

gh release create "v$Version" $MsiPath `
    --title "מוריד שיעורים v$Version" `
    --notes $releaseNotes `
    --latest

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " Release v$Version created successfully!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Cyan
} else {
    Write-Host "Failed to create release." -ForegroundColor Red
    exit 1
}
