# Terraform Infrastructure for Airpath

This directory contains Terraform configuration to deploy Airpath API to AWS.

## Architecture

The infrastructure includes:

- **VPC** with public and private subnets across 2 availability zones
- **RDS PostgreSQL** in private subnets with Multi-AZ option
- **ECS Fargate** for running Docker containers
- **Application Load Balancer** for distributing traffic
- **ECR** for Docker image storage
- **Secrets Manager** for secure credential storage
- **CloudWatch** for logging and monitoring
- **Security Groups** for network isolation

## Prerequisites

1. **AWS CLI** configured with appropriate credentials
   ```bash
   aws configure
   ```

2. **Terraform** installed (>= 1.0)
   ```bash
   terraform version
   ```

3. **Docker** installed for building images

4. **ACM Certificate** for HTTPS (create manually in AWS Console)
   - Request certificate for `*.airpath.travel`
   - Validate via DNS
   - Update `certificate_arn` in `alb.tf`

## Initial Setup

### 1. Create S3 Backend (Optional but Recommended)

```bash
# Create S3 bucket for Terraform state
aws s3api create-bucket \
  --bucket airpath-terraform-state \
  --region us-east-1

# Enable versioning
aws s3api put-bucket-versioning \
  --bucket airpath-terraform-state \
  --versioning-configuration Status=Enabled

# Create DynamoDB table for state locking
aws dynamodb create-table \
  --table-name terraform-state-lock \
  --attribute-definitions AttributeName=LockID,AttributeType=S \
  --key-schema AttributeName=LockID,KeyType=HASH \
  --billing-mode PAY_PER_REQUEST \
  --region us-east-1
```

Then uncomment the `backend` block in `main.tf`.

### 2. Configure Variables

```bash
# Copy example file
cp terraform.tfvars.example terraform.tfvars

# Edit terraform.tfvars with your values
# IMPORTANT: Generate strong passwords and secrets!
```

**Required changes in `terraform.tfvars`:**
- `db_password` - Strong database password
- `origin_secret` - Random secret for CloudFront validation
- `db_multi_az` - Set to `true` for production

### 3. Initialize Terraform

```bash
terraform init
```

## Deployment

### Step 1: Plan Infrastructure

```bash
terraform plan
```

Review the plan carefully to see what will be created.

### Step 2: Apply Infrastructure

```bash
terraform apply
```

Type `yes` when prompted. This will take **10-15 minutes** (RDS takes longest).

### Step 3: Build and Push Docker Image

After infrastructure is created, get the ECR repository URL:

```bash
terraform output ecr_repository_url
```

Build and push your image:

```powershell
# Login to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin $(terraform output -raw ecr_repository_url)

# Build image
docker build -t airpath-api:latest -f ../../Tripppi.Travel.API/Dockerfile ../..

# Tag image
docker tag airpath-api:latest $(terraform output -raw ecr_repository_url):latest

# Push image
docker push $(terraform output -raw ecr_repository_url):latest
```

### Step 4: Update ECS Service

After first image push, update the service:

```bash
aws ecs update-service \
  --cluster $(terraform output -raw ecs_cluster_name) \
  --service $(terraform output -raw ecs_service_name) \
  --force-new-deployment \
  --region us-east-1
```

### Step 5: Load Database

Get RDS endpoint:

```bash
terraform output rds_address
```

Run the database build script:

```powershell
../../infra/build-database.ps1 `
  -Host $(terraform output -raw rds_address) `
  -Username admin `
  -Password YOUR_DB_PASSWORD `
  -Database airpath
```

### Step 6: Configure DNS

Create a CNAME record in Route 53 or your DNS provider:

```
api.airpath.travel -> (ALB DNS from terraform output alb_dns_name)
```

## Accessing Your Application

After deployment:

```
https://api.airpath.travel/api/health/live
https://api.airpath.travel/api/airports/search?q=tor
```

## Monitoring

### View Logs

```bash
aws logs tail /ecs/airpath-api --follow --region us-east-1
```

### Check Service Status

```bash
aws ecs describe-services \
  --cluster $(terraform output -raw ecs_cluster_name) \
  --services $(terraform output -raw ecs_service_name) \
  --region us-east-1
```

### Check Task Status

```bash
aws ecs list-tasks \
  --cluster $(terraform output -raw ecs_cluster_name) \
  --service-name $(terraform output -raw ecs_service_name) \
  --region us-east-1
```

## Updating Infrastructure

1. Modify `.tf` files
2. Run `terraform plan` to review changes
3. Run `terraform apply` to apply changes

## Updating Application

1. Build new Docker image
2. Push to ECR with new tag
3. Update task definition or force new deployment:

```bash
aws ecs update-service \
  --cluster $(terraform output -raw ecs_cluster_name) \
  --service $(terraform output -raw ecs_service_name) \
  --force-new-deployment \
  --region us-east-1
```

## Cost Estimation

Monthly costs (US East 1, approximate):

| Resource | Cost |
|----------|------|
| RDS db.t4g.micro Multi-AZ | $31 |
| ECS Fargate (2 tasks) | $30 |
| ALB | $16 |
| NAT Gateway (2 AZs) | $65 |
| Data Transfer | Variable |
| **Total** | **~$142/month** |

To reduce costs for MVP:
- Set `db_multi_az = false` (-$16/month)
- Set `ecs_desired_count = 1` (-$15/month)
- Use 1 AZ only (-$32/month NAT)

## Destroying Infrastructure

**WARNING:** This will delete everything including databases!

```bash
terraform destroy
```

Type `yes` when prompted.

## Troubleshooting

### ECS Tasks Not Starting

Check logs:
```bash
aws logs tail /ecs/airpath-api --follow
```

### Database Connection Issues

Verify security groups allow ECS → RDS traffic:
```bash
aws ec2 describe-security-groups --group-ids $(terraform output -raw rds_security_group_id)
```

### ALB Health Check Failing

1. Verify health endpoint works: `/api/health/live`
2. Check ECS task logs
3. Verify security groups allow ALB → ECS traffic

## Security Best Practices

1. ✅ Never commit `terraform.tfvars` to version control
2. ✅ Use strong passwords for RDS
3. ✅ Enable Multi-AZ for production RDS
4. ✅ Enable deletion protection for production
5. ✅ Rotate secrets regularly
6. ✅ Review security group rules periodically
7. ✅ Enable CloudTrail for audit logs

## Support

For issues, check:
- CloudWatch Logs: `/ecs/airpath-api`
- ECS Service Events
- ALB Target Health
- RDS Monitoring tab in AWS Console
