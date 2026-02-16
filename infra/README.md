# Infrastructure & Deployment

This folder contains all infrastructure and deployment configurations for Airpath API.

## Files

- **deploy-to-aws.sh** - Bash deployment script for Linux/Mac
- **deploy-to-aws.ps1** - PowerShell deployment script for Windows
- **ecs-task-definition.json** - AWS ECS Fargate task definition template

## Prerequisites

1. **AWS CLI** installed and configured
   ```bash
   aws configure
   ```

2. **Docker** installed and running

3. **AWS Resources** created:
   - ECR repository: `airpath-api`
   - ECS cluster: `airpath-production`
   - ECS service: `airpath-api-service`
   - RDS PostgreSQL database
   - Secrets in AWS Secrets Manager:
     - `airpath/prod/db-connection`
     - `airpath/prod/origin-secret`

## Deployment

### Windows (PowerShell)
```powershell
.\infra\deploy-to-aws.ps1 -ImageTag v1.0.0
```

### Linux/Mac (Bash)
```bash
./infra/deploy-to-aws.sh v1.0.0
```

### First-Time Setup

1. **Create ECR repository:**
   ```bash
   aws ecr create-repository --repository-name airpath-api --region us-east-1
   ```

2. **Update task definition ARNs:**
   - Replace `ACCOUNT_ID` in `ecs-task-definition.json` with your AWS account ID

3. **Create secrets in Secrets Manager:**
   ```bash
   aws secretsmanager create-secret \
     --name airpath/prod/db-connection \
     --secret-string "Host=your-rds-endpoint;Port=5432;Database=airpath;Username=admin;Password=YOUR_PASSWORD;SSL Mode=Require"
   
   aws secretsmanager create-secret \
     --name airpath/prod/origin-secret \
     --secret-string "your-random-secret-here"
   ```

4. **Register task definition:**
   ```bash
   aws ecs register-task-definition --cli-input-json file://infra/ecs-task-definition.json
   ```

## Architecture

```
Internet → Route 53 (api.airpath.travel) → ALB → ECS Fargate → RDS PostgreSQL
```

See parent project documentation for full architecture details.

## Monitoring

**View logs:**
```bash
aws logs tail /ecs/airpath-api --follow --region us-east-1
```

**Check service status:**
```bash
aws ecs describe-services \
  --cluster airpath-production \
  --services airpath-api-service \
  --region us-east-1
```

## Rollback

To rollback to a previous version:
```bash
aws ecs update-service \
  --cluster airpath-production \
  --service airpath-api-service \
  --task-definition airpath-api-task:PREVIOUS_REVISION \
  --region us-east-1
```
