# DB Subnet Group (using PUBLIC subnets for publicly accessible RDS)
resource "aws_db_subnet_group" "main" {
  name       = "${var.project_name}-db-subnet-group"
  subnet_ids = aws_subnet.public[*].id

  tags = {
    Name = "${var.project_name}-db-subnet-group"
  }
}

# RDS PostgreSQL Instance
resource "aws_db_instance" "main" {
  identifier     = "${var.project_name}-${var.environment}-db"
  engine         = "postgres"
  engine_version = "16.6"
  instance_class = var.db_instance_class

  allocated_storage     = 20
  max_allocated_storage = 20  # Free tier: 20 GB max
  storage_type          = "gp2"  # Free tier uses gp2, not gp3
  storage_encrypted     = false  # Encryption costs extra

  db_name  = var.db_name
  username = var.db_username
  password = var.db_password

  multi_az               = false  # Free tier: Single-AZ only
  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.rds.id]
  publicly_accessible    = true  # Temporarily public for data migration

  backup_retention_period = 1  # Free tier: automated backups included, keep minimal
  backup_window           = "03:00-04:00"
  maintenance_window      = "mon:04:00-mon:05:00"

  enabled_cloudwatch_logs_exports = []  # Disable to save costs
  monitoring_interval             = 0  # Free tier: no enhanced monitoring
  monitoring_role_arn             = null  # Not needed without enhanced monitoring

  skip_final_snapshot       = true  # Free tier: skip final snapshot to avoid charges
  final_snapshot_identifier = null

  deletion_protection = false  # Easier to delete for development/testing

  tags = {
    Name = "${var.project_name}-${var.environment}-db"
  }
}

# IAM Role for RDS Enhanced Monitoring
# Commented out for free tier (enhanced monitoring costs extra)
# resource "aws_iam_role" "rds_monitoring" {
#   name = "${var.project_name}-rds-monitoring-role"
#
#   assume_role_policy = jsonencode({
#     Version = "2012-10-17"
#     Statement = [
#       {
#         Action = "sts:AssumeRole"
#         Effect = "Allow"
#         Principal = {
#           Service = "monitoring.rds.amazonaws.com"
#         }
#       }
#     ]
#   })
# }
#
# resource "aws_iam_role_policy_attachment" "rds_monitoring" {
#   role       = aws_iam_role.rds_monitoring.name
#   policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonRDSEnhancedMonitoringRole"
# }
