# AWS ECS Deployment Script for Airpath (PowerShell)
# Prerequisites: AWS CLI configured, Docker installed, ECR repository created

param(
    [string]$ImageTag = "latest",
    [string]$AwsRegion = "us-east-1"
)

$ErrorActionPreference = "Stop"

# Configuration
$EcrRepoName = "airpath-api"
$EcsCluster = "airpath-production"
$EcsService = "airpath-api-service"

Write-Host "🚀 Starting deployment to AWS ECS..." -ForegroundColor Green
Write-Host "   Region: $AwsRegion"

# Get AWS account ID
$AwsAccountId = (aws sts get-caller-identity --query Account --output text)
$EcrRegistry = "$AwsAccountId.dkr.ecr.$AwsRegion.amazonaws.com"
$ImageUri = "$EcrRegistry/${EcrRepoName}:$ImageTag"

Write-Host "   Image: $ImageUri"

# Step 1: Login to ECR
Write-Host "📦 Logging in to ECR..." -ForegroundColor Cyan
aws ecr get-login-password --region $AwsRegion | docker login --username AWS --password-stdin $EcrRegistry

# Step 2: Build Docker image
Write-Host "🔨 Building Docker image..." -ForegroundColor Cyan
docker build -t "${EcrRepoName}:$ImageTag" -f Tripppi.Travel.API/Dockerfile .

# Step 3: Tag image for ECR
Write-Host "🏷️  Tagging image..." -ForegroundColor Cyan
docker tag "${EcrRepoName}:$ImageTag" $ImageUri

# Step 4: Push to ECR
Write-Host "⬆️  Pushing image to ECR..." -ForegroundColor Cyan
docker push $ImageUri

# Step 5: Update ECS service (force new deployment)
Write-Host "🔄 Updating ECS service..." -ForegroundColor Cyan
aws ecs update-service `
    --cluster $EcsCluster `
    --service $EcsService `
    --force-new-deployment `
    --region $AwsRegion

Write-Host "✅ Deployment initiated successfully!" -ForegroundColor Green
Write-Host "   Monitor: aws ecs describe-services --cluster $EcsCluster --services $EcsService --region $AwsRegion"
