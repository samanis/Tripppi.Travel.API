# ECS/Fargate removed for free tier MVP
# Using EC2 t2.micro instead (see ec2.tf)
# ECS Fargate costs ~$15/month, EC2 t2.micro is free tier (750 hours/month)

# Keep ECR for Docker image storage (500 MB free forever)
resource "aws_ecr_repository" "main" {
  name                 = "${var.project_name}-api"
  image_tag_mutability = "MUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }

  tags = {
    Name = "${var.project_name}-api"
  }
}

# ECR Lifecycle Policy
resource "aws_ecr_lifecycle_policy" "main" {
  repository = aws_ecr_repository.main.name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last 5 images"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 5
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}

# CloudWatch Log Group (5 GB free forever)
resource "aws_cloudwatch_log_group" "app" {
  name              = "/aws/ec2/${var.project_name}-api"
  retention_in_days = 7

  tags = {
    Name = "${var.project_name}-api-logs"
  }
}
