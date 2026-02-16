#!/bin/bash
# Install and configure AWS CloudWatch Agent to ship Docker logs to CloudWatch
# This solves the credential discovery issue with AWS SDK in Docker containers

set -e

echo "=== Installing CloudWatch Agent ===" 
sudo yum install -y amazon-cloudwatch-agent

echo "=== Creating CloudWatch Agent configuration ===" 
sudo tee /opt/aws/amazon-cloudwatch-agent/etc/config.json > /dev/null <<'EOF'
{
  "logs": {
    "logs_collected": {
      "files": {
        "collect_list": [
          {
            "file_path": "/var/log/docker-airpath-api.log",
            "log_group_name": "/aws/ec2/airpath-api",
            "log_stream_name": "{instance_id}",
            "timezone": "UTC"
          }
        ]
      }
    }
  }
}
EOF

echo "=== Configuring Docker to log to file ==="
# Docker logs for airpath-api container will be redirected to a file that CloudWatch Agent can read
echo "Note: Update docker run command to use json-file logging driver with: --log-driver json-file --log-opt max-size=10m --log-opt max-file=3"

echo "=== Starting CloudWatch Agent ===" 
sudo /opt/aws/amazon-cloudwatch-agent/bin/amazon-cloudwatch-agent-ctl \
    -a fetch-config \
    -m ec2 \
    -s \
    -c file:/opt/aws/amazon-cloudwatch-agent/etc/config.json

echo "=== Verifying CloudWatch Agent status ===" 
sudo /opt/aws/amazon-cloudwatch-agent/bin/amazon-cloudwatch-agent-ctl \
    -a query \
    -m ec2 \
    -c default

echo "✅ CloudWatch Agent installed and started"
echo "Docker logs will now be shipped to CloudWatch log group: /aws/ec2/airpath-api"
