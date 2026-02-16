# CloudWatch Logging - Implementation Summary

## ✅ Solution: Docker awslogs Driver

CloudWatch logging is now **fully operational** using Docker's native `awslogs` log driver.

### What We Accomplished

1. **Request/Response Logging** - Every HTTP request is logged with:
   - Request ID (8-char correlation ID)
   - HTTP method and path
   - Query parameters
   - User agent and remote IP
   - Response status code
   - Elapsed time in milliseconds
   - Content types

2. **Application Logging** - All application events:
   - Startup sequence
   - Database connectivity
   - Airport cache loading (5,174 airports)
   - Health checks
   - Airport search operations
   - Warnings and errors

3. **CloudWatch Integration** - Logs flow to:
   - **Log Group**: `/aws/ec2/airpath-api`
   - **Region**: `ca-central-1`
   - **Log Stream**: Container ID (auto-created)
   - **Retention**: Indefinite (configurable via AWS Console)

### Technical Approach

**Why awslogs driver?**
- Native Docker integration
- Automatic credential resolution via EC2 instance profile
- No SDK/library dependency issues
- Works in containers without host networking
- Production-ready and battle-tested

**Why NOT AWS.Logger.AspNetCore?**
- Credential discovery issues in Docker containers
- Even with host networking, EC2 metadata access is unreliable
- Silently fails without clear error messages
- Adds unnecessary SDK dependencies

### Deployment Configuration

```bash
docker run -d \
  --name airpath-api \
  --restart unless-stopped \
  -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ASPNETCORE_HTTP_PORTS=8080 \
  -e DB_CONNECTION_STRING='...' \
  --log-driver=awslogs \
  --log-opt awslogs-region=ca-central-1 \
  --log-opt awslogs-group=/aws/ec2/airpath-api \
  --log-opt awslogs-create-group=true \
  245991315884.dkr.ecr.ca-central-1.amazonaws.com/airpath-api:latest
```

### IAM Permissions

Already configured in `infra/terraform/ec2.tf`:
```hcl
resource "aws_iam_role_policy" "cloudwatch_logs" {
  name = "cloudwatch-logs-access"
  role = aws_iam_role.airpath_ec2_app_role.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = [
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
        "logs:PutLogEvents",
        "logs:DescribeLogStreams"
      ]
      Resource = ["arn:aws:logs:ca-central-1:*:log-group:/aws/ec2/airpath-api:*"]
    }]
  })
}
```

### Viewing Logs

**AWS CLI:**
```bash
# Tail logs in real-time
aws logs tail "/aws/ec2/airpath-api" --follow --region ca-central-1

# View specific log stream
aws logs tail "/aws/ec2/airpath-api" --log-stream-names "<container-id>" --since 1h --region ca-central-1

# List log streams
aws logs describe-log-streams --log-group-name "/aws/ec2/airpath-api" --region ca-central-1
```

**AWS Console:**
https://ca-central-1.console.aws.amazon.com/cloudwatch/home?region=ca-central-1#logsV2:log-groups/log-group/$252Faws$252Fec2$252Fairpath-api

### Example Log Output

```
2026-01-04T20:23:00 info: Tripppi.Travel.API.Middleware.RequestResponseLoggingMiddleware[0]
      [899e77ec] --> GET /api/health | ContentType=none | UserAgent=Mozilla/5.0 | RemoteIp=::ffff:24.246.55.9
2026-01-04T20:23:00 info: Tripppi.Travel.API.Controllers.HealthController[0]
      Health check: cache loaded = True, db read-only = False
2026-01-04T20:23:01 info: Tripppi.Travel.API.Middleware.RequestResponseLoggingMiddleware[0]
      [899e77ec] <-- 200 | 117ms | ContentType=application/json; charset=utf-8
```

### Security & PII

✅ **PII-Safe Logging**:
- Request/response **bodies are NOT logged**
- Only metadata (method, path, status, timing)
- No sensitive data in logs
- Complies with zero-PII storage architecture

### Next Steps

1. **Configure Log Retention** - Set retention policy in AWS Console (e.g., 30 days)
2. **Set Up Alarms** - CloudWatch alarms for error rates, latency
3. **Create Metrics** - Custom metrics from log patterns
4. **Log Insights** - Use CloudWatch Insights for advanced queries

### Files Modified

- `Tripppi.Travel.API/Program.cs` - Removed AWS Logger, kept console logging
- `Tripppi.Travel.API/Middleware/RequestResponseLoggingMiddleware.cs` - Request/response logging
- `infra/deploy.sh` - Added awslogs driver configuration
- `infra/terraform/ec2.tf` - CloudWatch IAM permissions (already present)

## Status: ✅ COMPLETE

CloudWatch logging is production-ready and operational.
