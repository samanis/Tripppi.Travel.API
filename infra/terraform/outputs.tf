output "vpc_id" {
  description = "VPC ID"
  value       = aws_vpc.main.id
}

output "ecr_repository_url" {
  description = "ECR repository URL"
  value       = aws_ecr_repository.main.repository_url
}

output "ec2_public_ip" {
  description = "EC2 public IP address"
  value       = aws_eip.app.public_ip
}

output "ec2_instance_id" {
  description = "EC2 instance ID"
  value       = aws_instance.app.id
}

output "rds_endpoint" {
  description = "RDS endpoint"
  value       = aws_db_instance.main.endpoint
  sensitive   = true
}

output "rds_address" {
  description = "RDS address"
  value       = aws_db_instance.main.address
  sensitive   = true
}

output "db_connection_secret_arn" {
  description = "ARN of database connection secret in Secrets Manager"
  value       = aws_secretsmanager_secret.db_connection.arn
}

output "cloudwatch_log_group" {
  description = "CloudWatch log group name"
  value       = aws_cloudwatch_log_group.app.name
}

output "application_url" {
  description = "Application URL (use EC2 public IP)"
  value       = "http://${aws_eip.app.public_ip}"
}
