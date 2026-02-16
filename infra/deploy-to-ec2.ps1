# Deploy updated container to EC2 with CloudWatch logging
$ErrorActionPreference = "Stop"

Write-Host "`n🚀 Deploying updated container to EC2..." -ForegroundColor Cyan

# Save deployment script to temp file
$deployScript = @'
#!/bin/bash
set -e

echo "Stopping existing containers..."
docker stop $(docker ps -q) 2>/dev/null || true
docker rm $(docker ps -aq) 2>/dev/null || true

echo "Logging into ECR..."
aws ecr get-login-password --region ca-central-1 | docker login --username AWS --password-stdin 245991315884.dkr.ecr.ca-central-1.amazonaws.com

echo "Pulling latest image..."
docker pull 245991315884.dkr.ecr.ca-central-1.amazonaws.com/airpath-api:latest

echo "Starting container with Production environment..."
docker run -d \
  --name airpath-api \
  --restart unless-stopped \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_HTTP_PORTS=8080 \
  -e DB_CONNECTION_STRING='Host=10.0.1.41;Port=5432;Database=airpath;Username=airpathdb;Password=DqYsG0aRTg25pxo3IcBj;SSL Mode=Require;Trust Server Certificate=true' \
  245991315884.dkr.ecr.ca-central-1.amazonaws.com/airpath-api:latest

echo "Waiting for startup..."
sleep 5

echo "Container status:"
docker ps

echo "Testing health endpoint..."
curl -s http://localhost:8080/api/health || echo "Health check pending..."
'@

$tempScript = [System.IO.Path]::GetTempFileName() + ".sh"
$deployScript | Out-File -FilePath $tempScript -Encoding ASCII -NoNewline

try {
    Write-Host "📤 Uploading and executing deployment script..." -ForegroundColor Yellow
    
    if (-not $env:Path.Contains("Amazon\AWSCLIV2")) {
        $env:Path = "$env:Path;C:\Program Files\Amazon\AWSCLIV2"
    }
    
    # Get command ID
    $commandId = aws ssm send-command `
        --instance-ids i-017b1e82d45d1cd38 `
        --document-name "AWS-RunShellScript" `
        --parameters "commands=['bash -s <<EOF','$(Get-Content $tempScript -Raw)','EOF']" `
        --region ca-central-1 `
        --query 'Command.CommandId' `
        --output text
    
    Write-Host "✅ Command sent: $commandId" -ForegroundColor Green
    Write-Host "⏳ Waiting for deployment to complete..." -ForegroundColor Yellow
    
    Start-Sleep -Seconds 10
    
    # Check command status
    $status = aws ssm get-command-invocation `
        --command-id $commandId `
        --instance-id i-017b1e82d45d1cd38 `
        --region ca-central-1 `
        --output json | ConvertFrom-Json
    
    Write-Host "`n📋 Deployment output:" -ForegroundColor Cyan
    Write-Host $status.StandardOutputContent
    
    if ($status.StandardErrorContent) {
        Write-Host "`n⚠️  Errors:" -ForegroundColor Red
        Write-Host $status.StandardErrorContent
    }
    
    Write-Host "`n✅ Deployment complete!" -ForegroundColor Green
}
finally {
    Remove-Item $tempScript -ErrorAction SilentlyContinue
}
