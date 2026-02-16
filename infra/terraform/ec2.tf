# Free Tier EC2 Instance (t2.micro - 750 hours/month free)
# Get latest Amazon Linux 2023 AMI
data "aws_ami" "amazon_linux_2023" {
  most_recent = true
  owners      = ["amazon"]

  filter {
    name   = "name"
    values = ["al2023-ami-*-x86_64"]
  }

  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

# IAM role for EC2 to access ECR and Secrets Manager
resource "aws_iam_role" "ec2_app" {
  name = "${var.project_name}-ec2-app-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "ec2.amazonaws.com"
        }
      }
    ]
  })

  tags = {
    Name = "${var.project_name}-ec2-role"
  }
}

# IAM policy for ECR access
resource "aws_iam_role_policy" "ecr_access" {
  name = "ecr-access"
  role = aws_iam_role.ec2_app.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ecr:GetAuthorizationToken",
          "ecr:BatchCheckLayerAvailability",
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage"
        ]
        Resource = "*"
      }
    ]
  })
}

# IAM policy for Secrets Manager access
resource "aws_iam_role_policy" "secrets_access" {
  name = "secrets-access"
  role = aws_iam_role.ec2_app.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]
        Resource = [
          aws_secretsmanager_secret.db_connection.arn,
          "${aws_secretsmanager_secret.db_connection.arn}*"
        ]
      }
    ]
  })
}

# IAM policy for CloudWatch Logs access
resource "aws_iam_role_policy" "cloudwatch_logs" {
  name = "cloudwatch-logs-access"
  role = aws_iam_role.ec2_app.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents",
          "logs:DescribeLogStreams"
        ]
        Resource = [
          "arn:aws:logs:${var.aws_region}:*:log-group:/aws/ec2/airpath-api:*"
        ]
      }
    ]
  })
}

# Instance profile to attach IAM role to EC2
resource "aws_iam_instance_profile" "ec2_app" {
  name = "${var.project_name}-ec2-profile"
  role = aws_iam_role.ec2_app.name
}

# Create key pair for SSH access
resource "aws_key_pair" "deployer" {
  key_name   = "${var.project_name}-deployer-key"
  public_key = file("~/.ssh/id_rsa.pub")  # TODO: Generate if you don't have one

  tags = {
    Name = "${var.project_name}-deployer-key"
  }
}

# EC2 Instance
resource "aws_instance" "app" {
  ami           = data.aws_ami.amazon_linux_2023.id
  instance_type = "t2.micro"  # Free tier eligible
  key_name      = aws_key_pair.deployer.key_name

  subnet_id                   = aws_subnet.public[0].id
  vpc_security_group_ids      = [aws_security_group.ec2_app.id]
  associate_public_ip_address = true
  iam_instance_profile        = aws_iam_instance_profile.ec2_app.name

  root_block_device {
    volume_size = 30  # Free tier: 30 GB EBS (Amazon Linux 2023 requires minimum 30GB)
    volume_type = "gp2"
  }

  user_data = <<-EOF
              #!/bin/bash
              # Install Docker
              yum update -y
              yum install -y docker
              systemctl start docker
              systemctl enable docker
              usermod -aG docker ec2-user

              # Install Docker Compose
              curl -L "https://github.com/docker/compose/releases/latest/download/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
              chmod +x /usr/local/bin/docker-compose

              # Create app directory
              mkdir -p /home/ec2-user/app
              chown ec2-user:ec2-user /home/ec2-user/app

              # Setup nginx reverse proxy (optional)
              yum install -y nginx
              cat > /etc/nginx/nginx.conf <<'NGINX'
              events {
                  worker_connections 1024;
              }
              http {
                  server {
                      listen 80;
                      location / {
                          proxy_pass http://localhost:8080;
                          proxy_set_header Host $host;
                          proxy_set_header X-Real-IP $remote_addr;
                      }
                  }
              }
NGINX
              systemctl start nginx
              systemctl enable nginx
              EOF

  tags = {
    Name = "${var.project_name}-app-server"
  }
}

# Elastic IP for EC2 (free when attached to running instance)
resource "aws_eip" "app" {
  instance = aws_instance.app.id
  domain   = "vpc"

  tags = {
    Name = "${var.project_name}-app-eip"
  }
}
