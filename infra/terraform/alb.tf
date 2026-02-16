# ALB removed for free tier MVP - using direct EC2 access instead
# ALB costs $16/month, EC2 t2.micro is free tier eligible (750 hours/month)
# For production, uncomment resources below and update security_groups.tf

# # Application Load Balancer
# resource "aws_lb" "main" {
#   name               = "${var.project_name}-alb"
#   internal           = false
#   load_balancer_type = "application"
#   security_groups    = [aws_security_group.alb.id]
#   subnets            = aws_subnet.public[*].id
#
#   enable_deletion_protection = var.environment == "production"
#
#   tags = {
#     Name = "${var.project_name}-alb"
#   }
# }
#
# # Target Group
# resource "aws_lb_target_group" "main" {
#   name        = "${var.project_name}-tg"
#   port        = 8080
#   protocol    = "HTTP"
#   vpc_id      = aws_vpc.main.id
#   target_type = "ip"
#
#   health_check {
#     enabled             = true
#     healthy_threshold   = 2
#     unhealthy_threshold = 3
#     timeout             = 5
#     interval            = 30
#     path                = "/api/health/live"
#     matcher             = "200"
#   }
#
#   deregistration_delay = 30
#
#   tags = {
#     Name = "${var.project_name}-tg"
#   }
# }
#
# # HTTP Listener (forward to target group directly - add HTTPS redirect later)
# resource "aws_lb_listener" "http" {
#   load_balancer_arn = aws_lb.main.arn
#   port              = 80
#   protocol          = "HTTP"
#
#   default_action {
#     type             = "forward"
#     target_group_arn = aws_lb_target_group.main.arn
#   }
# }
#
# # HTTPS Listener
# # NOTE: You need to create ACM certificate manually and uncomment this block
# # resource "aws_lb_listener" "https" {
# #   load_balancer_arn = aws_lb.main.arn
# #   port              = 443
# #   protocol          = "HTTPS"
# #   ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
# #   certificate_arn   = "arn:aws:acm:${var.aws_region}:ACCOUNT_ID:certificate/CERTIFICATE_ID" # TODO: Replace with your ACM certificate ARN
# #
# #   default_action {
# #     type             = "forward"
# #     target_group_arn = aws_lb_target_group.main.arn
# #   }
# # }
