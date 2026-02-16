#!/bin/bash
set -e

echo "Stopping existing containers..."
docker stop $(docker ps -q) 2>/dev/null || true
docker rm $(docker ps -aq) 2>/dev/null || true

echo "Logging into ECR..."
aws ecr get-login-password --region ca-central-1 | docker login --username AWS --password-stdin 245991315884.dkr.ecr.ca-central-1.amazonaws.com

echo "Pulling latest image..."
docker pull 245991315884.dkr.ecr.ca-central-1.amazonaws.com/airpath-api:latest

echo "Starting new container with Production environment..."
docker run -d \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_HTTP_PORTS=8080 \
  -e DB_CONNECTION_STRING="Host=10.0.1.41;Port=5432;Database=airpath;Username=airpathdb;Password=DqYsG0aRTg25pxo3IcBj;SSL Mode=Require;Trust Server Certificate=true" \
  245991315884.dkr.ecr.ca-central-1.amazonaws.com/airpath-api:latest

echo "Deployment complete! Checking container status..."
sleep 3
docker ps
