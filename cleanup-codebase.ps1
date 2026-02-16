#!/usr/bin/env pwsh
# Comprehensive codebase cleanup for Tripppi.Travel.API
# Removes unused template files while preserving architecture

$ErrorActionPreference = "Stop"

Write-Host "`n??????????????????????????????????????????????" -ForegroundColor Cyan
Write-Host "?  Tripppi.Travel.API - Codebase Cleanup    ?" -ForegroundColor Cyan
Write-Host "?  Removing unused template files            ?" -ForegroundColor Cyan
Write-Host "??????????????????????????????????????????????`n" -ForegroundColor Cyan

# Files to delete
$filesToDelete = @(
    "Tripppi.Travel.API\WeatherForecast.cs",
    "Tripppi.Travel.API\Controllers\WeatherForecastController.cs",
    "Tripppi.Travel.Api.Test\UnitTest1.cs"
)

# Track results
$deleted = @()
$notFound = @()

foreach ($file in $filesToDelete) {
    if (Test-Path $file) {
        Write-Host "[DELETE] $file" -ForegroundColor Yellow
        Remove-Item -Path $file -Force
        $deleted += $file
    } else {
        Write-Host "[SKIP]   $file (not found)" -ForegroundColor Gray
        $notFound += $file
    }
}

# Summary
Write-Host "`n??????????????????????????????????????????????" -ForegroundColor Green
Write-Host "?  Cleanup Complete                          ?" -ForegroundColor Green
Write-Host "?  Deleted: $($deleted.Count) files                         ?" -ForegroundColor Green
Write-Host "?  Not Found: $($notFound.Count) files                       ?" -ForegroundColor Green
Write-Host "??????????????????????????????????????????????`n" -ForegroundColor Green

if ($deleted.Count -gt 0) {
    Write-Host "Deleted files:" -ForegroundColor Cyan
    $deleted | ForEach-Object { Write-Host "  • $_" -ForegroundColor Gray }
    Write-Host ""
}

Write-Host "? Preserved files per architecture:" -ForegroundColor Cyan
Write-Host "  • IFlightsInfo.cs (core contract for Duffel)" -ForegroundColor Gray
Write-Host "  • All domain models and repositories" -ForegroundColor Gray
Write-Host "  • Real test files (EfAirportRepositoryTests, etc.)" -ForegroundColor Gray
Write-Host ""

Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Run: dotnet build --no-restore" -ForegroundColor Gray
Write-Host "  2. Run: dotnet test --no-build" -ForegroundColor Gray
Write-Host "  3. Commit: git add . && git commit -m 'chore: remove unused template files'" -ForegroundColor Gray
Write-Host ""