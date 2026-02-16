# Database Connection String Secret
resource "aws_secretsmanager_secret" "db_connection" {
  name        = "${var.project_name}/${var.environment}/db-connection"
  description = "PostgreSQL connection string for ${var.project_name}"

  recovery_window_in_days = var.environment == "production" ? 30 : 0

  tags = {
    Name = "${var.project_name}-db-connection"
  }
}

resource "aws_secretsmanager_secret_version" "db_connection" {
  secret_id = aws_secretsmanager_secret.db_connection.id
  secret_string = "Host=${aws_db_instance.main.address};Port=5432;Database=${var.db_name};Username=${var.db_username};Password=${var.db_password};SSL Mode=Require;Trust Server Certificate=false"
}

# Origin Secret for CloudFront Validation
resource "aws_secretsmanager_secret" "origin_secret" {
  name        = "${var.project_name}/${var.environment}/origin-secret"
  description = "CloudFront origin validation secret"

  recovery_window_in_days = var.environment == "production" ? 30 : 0

  tags = {
    Name = "${var.project_name}-origin-secret"
  }
}

resource "aws_secretsmanager_secret_version" "origin_secret" {
  secret_id     = aws_secretsmanager_secret.origin_secret.id
  secret_string = var.origin_secret
}
