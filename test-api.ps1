#!/usr/bin/env pwsh
# API Test Script for MeirDownloader

param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Continue"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " MeirDownloader API Test Script" -ForegroundColor Cyan
Write-Host " Base URL: $BaseUrl" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$passed = 0
$failed = 0

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url
    )
    
    Write-Host "Testing: $Name" -ForegroundColor Yellow
    Write-Host "  URL: $Url" -ForegroundColor Gray
    
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 30
        $statusCode = $response.StatusCode
        
        if ($statusCode -eq 200) {
            $json = $response.Content | ConvertFrom-Json
            $count = if ($json -is [System.Array]) { $json.Count } else { 1 }
            Write-Host "  Status: $statusCode OK" -ForegroundColor Green
            Write-Host "  Items returned: $count" -ForegroundColor Green
            $script:passed++
        } else {
            Write-Host "  Status: $statusCode" -ForegroundColor Red
            $script:failed++
        }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode) {
            Write-Host "  Status: $statusCode" -ForegroundColor Red
            try {
                $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
                $body = $reader.ReadToEnd()
                Write-Host "  Response: $body" -ForegroundColor Red
            } catch {}
        } else {
            Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Red
        }
        $script:failed++
    }
    Write-Host ""
}

# Test health / connectivity first
Write-Host "Checking API connectivity..." -ForegroundColor Yellow
try {
    $null = Invoke-WebRequest -Uri "$BaseUrl/swagger/index.html" -UseBasicParsing -TimeoutSec 5
    Write-Host "  API is running." -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "  WARNING: API may not be running at $BaseUrl" -ForegroundColor Red
    Write-Host "  Start it first: dotnet run --project MeirDownloader.Api" -ForegroundColor Yellow
    Write-Host ""
}

# Test endpoints
Test-Endpoint -Name "GET /api/rabbis" -Url "$BaseUrl/api/rabbis"
Test-Endpoint -Name "GET /api/series" -Url "$BaseUrl/api/series"
Test-Endpoint -Name "GET /api/series?rabbiId=1" -Url "$BaseUrl/api/series?rabbiId=1"
Test-Endpoint -Name "GET /api/lessons?page=1" -Url "$BaseUrl/api/lessons?page=1"
Test-Endpoint -Name "GET /api/lessons?rabbiId=1&page=1" -Url "$BaseUrl/api/lessons?rabbiId=1&page=1"

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Test Results" -ForegroundColor Cyan
Write-Host "  Passed: $passed" -ForegroundColor $(if ($passed -gt 0) { "Green" } else { "Gray" })
Write-Host "  Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })
Write-Host "  Total:  $($passed + $failed)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

if ($failed -gt 0) {
    Write-Host ""
    Write-Host "NOTE: Failures may be due to the external meirtv.com API being" -ForegroundColor Yellow
    Write-Host "unavailable or changed. This is not necessarily a bug in the code." -ForegroundColor Yellow
    exit 1
}
