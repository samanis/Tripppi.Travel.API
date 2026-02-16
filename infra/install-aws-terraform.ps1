# Run this script as Administrator (Right-click PowerShell → Run as Administrator)
# This script installs AWS CLI and Terraform

Write-Host "Installing AWS CLI and Terraform..." -ForegroundColor Green

# Install using Chocolatey
choco install awscli terraform -y

# Verify installations
Write-Host "`nVerifying installations..." -ForegroundColor Green

try {
    $awsVersion = aws --version 2>&1
    Write-Host "✓ AWS CLI installed: $awsVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ AWS CLI not found" -ForegroundColor Red
}

try {
    $tfVersion = terraform version
    Write-Host "✓ Terraform installed: $tfVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ Terraform not found" -ForegroundColor Red
}

Write-Host "`n" -ForegroundColor Green
Write-Host "Installation complete!" -ForegroundColor Green
Write-Host "Please close and reopen your PowerShell terminal for PATH changes to take effect." -ForegroundColor Yellow
Write-Host "`nNext steps:" -ForegroundColor Cyan
Write-Host "1. Close this terminal" -ForegroundColor White
Write-Host "2. Open a new PowerShell terminal (no admin needed)" -ForegroundColor White
Write-Host "3. Run: aws --version" -ForegroundColor White
Write-Host "4. Run: terraform version" -ForegroundColor White
Write-Host "5. Run: aws configure" -ForegroundColor White

Read-Host -Prompt "`nPress Enter to exit"
