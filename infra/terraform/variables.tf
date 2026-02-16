variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

variable "environment" {
  description = "Environment name"
  type        = string
  default     = "production"
}

variable "project_name" {
  description = "Project name"
  type        = string
  default     = "airpath"
}

# VPC Configuration
variable "vpc_cidr" {
  description = "CIDR block for VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "availability_zones" {
  description = "Availability zones"
  type        = list(string)
  default     = ["ca-central-1a"]  # Free tier: single AZ
}

# RDS Configuration
variable "db_instance_class" {
  description = "RDS instance class"
  type        = string
  default     = "db.t4g.micro"
}

variable "db_name" {
  description = "Database name"
  type        = string
  default     = "airpath"
}

variable "db_username" {
  description = "Database master username"
  type        = string
  default     = "airpathdb"
}

variable "db_password" {
  description = "Database master password"
  type        = string
  sensitive   = true
}

variable "db_multi_az" {
  description = "Enable Multi-AZ for RDS"
  type        = bool
  default     = false # Set to true for production
}

# ECS Configuration
variable "ecs_task_cpu" {
  description = "ECS task CPU units"
  type        = string
  default     = "512" # 0.5 vCPU
}

variable "ecs_task_memory" {
  description = "ECS task memory in MB"
  type        = string
  default     = "1024" # 1 GB
}

variable "ecs_desired_count" {
  description = "Desired number of ECS tasks"
  type        = number
  default     = 0  # Free tier: using EC2 t2.micro instead
}

variable "container_image" {
  description = "Docker container image"
  type        = string
  default     = "" # Will be set to ECR repository URL after first push
}

# Application Configuration
variable "allowed_origin" {
  description = "CORS allowed origin"
  type        = string
  default     = "https://airpath.travel"
}

variable "origin_secret" {
  description = "Secret for CloudFront origin validation"
  type        = string
  sensitive   = true
}
