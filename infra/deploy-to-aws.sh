#!/bin/bash
# AWS ECS Deployment Script for Airpath
# Prerequisites: AWS CLI configured, Docker installed, ECR repository created

set -e

# Configuration
AWS_REGION="us-east-1"
ECR_REPO_NAME="airpath-api"
ECS_CLUSTER="airpath-production"
ECS_SERVICE="airpath-api-service"
IMAGE_TAG="${1:-latest}"

# Get AWS account ID
AWS_ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
ECR_REGISTRY="${AWS_ACCOUNT_ID}.dkr.ecr.${AWS_REGION}.amazonaws.com"
IMAGE_URI="${ECR_REGISTRY}/${ECR_REPO_NAME}:${IMAGE_TAG}"

echo "🚀 Starting deployment to AWS ECS..."
echo "   Region: ${AWS_REGION}"
echo "   Image: ${IMAGE_URI}"

# Step 1: Login to ECR
echo "📦 Logging in to ECR..."
aws ecr get-login-password --region ${AWS_REGION} | docker login --username AWS --password-stdin ${ECR_REGISTRY}

# Step 2: Build Docker image
echo "🔨 Building Docker image..."
docker build -t ${ECR_REPO_NAME}:${IMAGE_TAG} -f Tripppi.Travel.API/Dockerfile .

# Step 3: Tag image for ECR
echo "🏷️  Tagging image..."
docker tag ${ECR_REPO_NAME}:${IMAGE_TAG} ${IMAGE_URI}

# Step 4: Push to ECR
echo "⬆️  Pushing image to ECR..."
docker push ${IMAGE_URI}

# Step 5: Update ECS service (force new deployment)
echo "🔄 Updating ECS service..."
aws ecs update-service \
    --cluster ${ECS_CLUSTER} \
    --service ${ECS_SERVICE} \
    --force-new-deployment \
    --region ${AWS_REGION}

echo "✅ Deployment initiated successfully!"
echo "   Monitor progress: aws ecs describe-services --cluster ${ECS_CLUSTER} --services ${ECS_SERVICE} --region ${AWS_REGION}"
