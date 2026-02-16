#!/bin/bash
# Deploy Airpath API to EC2 instance

set -e

echo "🚀 Deploying Airpath API to EC2..."

# Configuration
ECR_REGISTRY="245991315884.dkr.ecr.ca-central-1.amazonaws.com"
ECR_REPO="airpath-api"
IMAGE_TAG="latest"
CONTAINER_NAME="airpath-api"
APP_PORT=8080

# RDS connection string (from terraform outputs)
DB_CONNECTION="Host=airpath-production-db.cl04ie2ior6k.ca-central-1.amazonaws.com;Port=5432;Database=airpath;Username=airpathdb;Password=DqYsG0aRTg25pxo3IcBj;SSL Mode=Require;Trust Server Certificate=true"

echo "🔐 Authenticating with ECR..."
aws ecr get-login-password --region ca-central-1 | \
  docker login --username AWS --password-stdin ${ECR_REGISTRY}

echo "📥 Pulling latest image..."
docker pull ${ECR_REGISTRY}/${ECR_REPO}:${IMAGE_TAG}

echo "🛑 Stopping existing container (if any)..."
docker stop ${CONTAINER_NAME} 2>/dev/null || true
docker rm ${CONTAINER_NAME} 2>/dev/null || true

echo "🐳 Starting new container..."
docker run -d \
  --name ${CONTAINER_NAME} \
  --restart unless-stopped \
  -p ${APP_PORT}:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="${DB_CONNECTION}" \
  -e ASPNETCORE_URLS="http://+:8080" \
  ${ECR_REGISTRY}/${ECR_REPO}:${IMAGE_TAG}

echo "⏳ Waiting for container to start..."
sleep 5

echo "🔍 Container status:"
docker ps --filter "name=${CONTAINER_NAME}" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo "📋 Recent logs:"
docker logs --tail 20 ${CONTAINER_NAME}

echo "✅ Deployment complete!"
echo "🌐 API should be available at http://localhost:${APP_PORT}/api/health"
