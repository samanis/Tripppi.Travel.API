#!/bin/bash
set -e
echo "=== Stopping existing containers ==="
docker stop $(docker ps -q) 2>/dev/null || true
docker rm $(docker ps -aq) 2>/dev/null || true

echo "=== Logging into ECR ==="
aws ecr get-login-password --region ca-central-1 | docker login --username AWS --password-stdin 245991315884.dkr.ecr.ca-central-1.amazonaws.com

echo "=== Pulling latest image ==="
docker pull 245991315884.dkr.ecr.ca-central-1.amazonaws.com/airpath-api:latest

echo "=== Starting container with Production environment and CloudWatch logging ==="
docker run -d \
  --name airpath-api \
  --restart unless-stopped \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_HTTP_PORTS=8080 \
  -e DB_CONNECTION_STRING='Host=10.0.1.41;Port=5432;Database=airpath;Username=airpathdb;Password=DqYsG0aRTg25pxo3IcBj;SSL Mode=Require;Trust Server Certificate=true' \
  --log-driver=awslogs \
  --log-opt awslogs-region=ca-central-1 \
  --log-opt awslogs-group=/aws/ec2/airpath-api \
  --log-opt awslogs-create-group=true \
  245991315884.dkr.ecr.ca-central-1.amazonaws.com/airpath-api:latest

echo "=== Waiting for startup ==="
sleep 5

echo "=== Container status ==="
docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

echo "=== Testing health endpoint ==="
curl -s http://localhost:8080/api/health | head -n 20

echo "=== Deployment complete ==="