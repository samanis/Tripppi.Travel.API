# Manual RDS Setup Guide for Airpath

Since AWS CLI and Terraform are not yet installed, here's the fastest way to migrate your database to AWS.

## Step 1: Create RDS PostgreSQL Instance

### Via AWS Console

1. **Login to AWS Console**: https://console.aws.amazon.com
2. **Navigate to RDS**: Services → Database → RDS
3. **Click "Create database"**

### Database Settings

**Engine options:**
- Engine type: `PostgreSQL`
- Engine version: `16.1` (or latest 16.x)
- Template: `Free tier` (for testing) or `Production` (for real use)

**Settings:**
- DB instance identifier: `airpath-production-db`
- Master username: `admin`
- Master password: `<STRONG_PASSWORD>` (save this!)
- Confirm password

**Instance configuration:**
- DB instance class: `db.t4g.micro` (2 vCPU, 1 GB RAM)
- Storage type: `General Purpose SSD (gp3)`
- Allocated storage: `20 GB`
- Enable storage autoscaling: ✅ (max 100 GB)

**Connectivity:**
- Compute resource: `Don't connect to an EC2 compute resource`
- VPC: `Default VPC` (or create new)
- Public access: `Yes` ⚠️ (for initial migration, secure later)
- VPC security group: `Create new`
  - Name: `airpath-rds-sg`
- Availability Zone: `No preference`

**Database authentication:**
- Password authentication: ✅

**Additional configuration:**
- Initial database name: `airpath`
- DB parameter group: `default.postgres16`
- Backup retention period: `7 days`
- Enable encryption: ✅
- Enable Enhanced monitoring: ✅ (60 seconds)
- Enable deletion protection: ✅ (for production only)

4. **Click "Create database"**

⏱️ **Wait time: 10-15 minutes** for RDS to be available.

## Step 2: Configure Security Group

After RDS is created:

1. Click on your RDS instance
2. Click on the VPC security group
3. **Edit inbound rules**:
   - Type: `PostgreSQL`
   - Port: `5432`
   - Source: `My IP` (your current IP address)
   - Description: `Allow PostgreSQL from my machine`
4. **Save rules**

⚠️ **Security Note**: For production, restrict access to specific IPs or use VPN/bastion host.

## Step 3: Get RDS Endpoint

1. Go to RDS → Databases → airpath-production-db
2. Copy the **Endpoint** (looks like: `airpath-production-db.abc123.us-east-1.rds.amazonaws.com`)
3. Note the **Port** (usually `5432`)

## Step 4: Install PostgreSQL Client (psql)

### Windows

**Option A: Download PostgreSQL Installer**
1. Visit: https://www.postgresql.org/download/windows/
2. Download installer (includes psql)
3. Run installer
4. Add `C:\Program Files\PostgreSQL\16\bin` to PATH

**Option B: Chocolatey** (if installed)
```powershell
choco install postgresql
```

**Verify installation:**
```powershell
psql --version
```

## Step 5: Test Connection

```powershell
# Replace with your RDS endpoint and password
$env:PGPASSWORD = "YOUR_DB_PASSWORD"
psql -h airpath-production-db.abc123.us-east-1.rds.amazonaws.com `
     -U admin `
     -d airpath `
     -c "SELECT version();"
```

If successful, you'll see PostgreSQL version info.

## Step 6: Run Database Migration

Navigate to your project directory and run:

```powershell
cd C:\Users\saman\source\repos\Tripppi.Travel.API\infra

# Run the database build script
.\build-database.ps1 `
  -Host "airpath-production-db.abc123.us-east-1.rds.amazonaws.com" `
  -Port 5432 `
  -Username "admin" `
  -Password "YOUR_DB_PASSWORD" `
  -Database "airpath"
```

This will:
1. Create the `airports` table
2. Create indexes (IATA, ICAO, city, full-text search)
3. Import 5,174 airports from CSV
4. Run VACUUM ANALYZE for optimization
5. Verify the data

⏱️ **Migration time: 1-2 minutes**

## Step 7: Verify Migration

```powershell
$env:PGPASSWORD = "YOUR_DB_PASSWORD"
psql -h airpath-production-db.abc123.us-east-1.rds.amazonaws.com `
     -U admin `
     -d airpath `
     -c "SELECT COUNT(*) as airport_count FROM airports;"
```

Expected output: `5174`

```powershell
# Test a search query
psql -h airpath-production-db.abc123.us-east-1.rds.amazonaws.com `
     -U admin `
     -d airpath `
     -c "SELECT airport_name, airport_iata, city_name FROM airports WHERE airport_iata ILIKE 'YYZ%' LIMIT 5;"
```

## Step 8: Update Application Configuration

### For Local Testing with RDS

Update `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=airpath-production-db.abc123.us-east-1.rds.amazonaws.com;Port=5432;Database=airpath;Username=admin;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
  }
}
```

Test locally:
```powershell
cd C:\Users\saman\source\repos\Tripppi.Travel.API
dotnet run --launch-profile http
```

Then test the API:
```powershell
Invoke-RestMethod "http://localhost:5063/api/airports/search?q=tor" | ConvertTo-Json
```

## Security Hardening (After Migration)

### 1. Restrict Public Access

If your application will run on ECS:
1. Go to RDS → Modify
2. Set **Public access** to `No`
3. Update security group to allow access only from ECS security group

### 2. Rotate Password

Store password in AWS Secrets Manager instead of hardcoding:
1. Go to Secrets Manager → Store a new secret
2. Secret type: `Credentials for RDS database`
3. Select your RDS instance
4. Secret name: `airpath/prod/db-connection`

### 3. Enable SSL Certificate Verification

Download RDS SSL certificate:
```powershell
Invoke-WebRequest -Uri "https://truststore.pki.rds.amazonaws.com/global/global-bundle.pem" `
                  -OutFile "rds-ca-bundle.pem"
```

Update connection string:
```
...;SSL Mode=Require;Trust Server Certificate=false;Root Certificate=./rds-ca-bundle.pem
```

## Cost Monitoring

- **db.t4g.micro**: ~$15.50/month (Single-AZ)
- **Storage (20 GB)**: ~$2.30/month
- **Backup (7 days)**: ~$0.95/month
- **Data transfer**: Variable

**Total: ~$18-20/month** for Single-AZ

For Multi-AZ (production): ~$31/month

## Troubleshooting

### Connection Timeout

- Verify security group allows your IP on port 5432
- Check RDS is `Available` status
- Verify VPC settings allow outbound connections

### Authentication Failed

- Double-check username (`admin`)
- Verify password (case-sensitive)
- Check database name (`airpath`)

### CSV Import Fails

- Verify CSV file exists at `../Tripppi.Travel.API/bin/data/major_airports_enriched.csv`
- Check file encoding (should be UTF-8)
- Verify all 5,174 rows are present

## Next Steps

After successful migration:

1. ✅ Test all API endpoints with RDS
2. ✅ Deploy application to ECS/EC2
3. ✅ Restrict RDS public access
4. ✅ Set up CloudWatch alarms (CPU, storage, connections)
5. ✅ Configure automated backups
6. ✅ Consider Multi-AZ for production

## Rollback Plan

If migration fails, your local database is unchanged:
1. Keep local PostgreSQL running
2. RDS can be deleted without affecting local setup
3. No application code changes needed to switch back
