#!/usr/bin/env pwsh
# Migrate Airport Data to AWS RDS PostgreSQL

param(
    [Parameter(Mandatory=$true)]
    [string]$RdsEndpoint,
    
    [Parameter(Mandatory=$true)]
    [string]$RdsUsername,
    
    [Parameter(Mandatory=$true)]
    [string]$RdsPassword,
    
    [string]$RdsDatabase = "airpath",
    [string]$CsvPath = "Tripppi.Travel.API/bin/data/major_airports_enriched.csv"
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting airport data migration to AWS RDS..." -ForegroundColor Green

# Build connection string
$ConnectionString = "Host=$RdsEndpoint;Port=5432;Database=$RdsDatabase;Username=$RdsUsername;Password=$RdsPassword;SSL Mode=Require"

Write-Host "📋 Step 1: Creating database schema..." -ForegroundColor Cyan

# Create schema SQL
$SchemaSql = @"
-- Drop table if exists (be careful in production!)
DROP TABLE IF EXISTS airports;

-- Create airports table
CREATE TABLE airports (
    airport_name VARCHAR(255),
    airport_iata VARCHAR(10),
    airport_icao VARCHAR(10),
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    city_name VARCHAR(255),
    city_iata_code VARCHAR(10),
    city_icao_code VARCHAR(10),
    country_name VARCHAR(100),
    country_icao VARCHAR(10),
    country_iata VARCHAR(10),
    popularity_score INTEGER
);

-- Create indexes for performance
CREATE INDEX idx_airports_iata ON airports(airport_iata);
CREATE INDEX idx_airports_name ON airports(airport_name);
CREATE INDEX idx_airports_city ON airports(city_name);
CREATE INDEX idx_airports_country ON airports(country_name);
CREATE INDEX idx_airports_popularity ON airports(popularity_score DESC);
"@

# Save schema to temp file
$TempSchemaFile = [System.IO.Path]::GetTempFileName() + ".sql"
$SchemaSql | Out-File -FilePath $TempSchemaFile -Encoding UTF8

# Execute schema creation
$env:PGPASSWORD = $RdsPassword
psql -h $RdsEndpoint -U $RdsUsername -d $RdsDatabase -f $TempSchemaFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Schema creation failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Schema created successfully" -ForegroundColor Green

Write-Host "📊 Step 2: Loading airport data from CSV..." -ForegroundColor Cyan

# Copy CSV data to RDS
$CopySql = "\COPY airports(airport_name, airport_iata, airport_icao, latitude, longitude, city_name, city_iata_code, city_icao_code, country_name, country_icao, country_iata, popularity_score) FROM '$CsvPath' WITH (FORMAT csv, HEADER true);"

$TempCopyFile = [System.IO.Path]::GetTempFileName() + ".sql"
$CopySql | Out-File -FilePath $TempCopyFile -Encoding UTF8

psql -h $RdsEndpoint -U $RdsUsername -d $RdsDatabase -f $TempCopyFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Data import failed!" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Data imported successfully" -ForegroundColor Green

Write-Host "🔍 Step 3: Verifying data..." -ForegroundColor Cyan

# Verify row count
$CountSql = "SELECT COUNT(*) as row_count FROM airports;"
$TempCountFile = [System.IO.Path]::GetTempFileName() + ".sql"
$CountSql | Out-File -FilePath $TempCountFile -Encoding UTF8

psql -h $RdsEndpoint -U $RdsUsername -d $RdsDatabase -f $TempCountFile

# Clean up temp files
Remove-Item $TempSchemaFile, $TempCopyFile, $TempCountFile -ErrorAction SilentlyContinue

Write-Host "`n✅ Migration completed successfully!" -ForegroundColor Green
Write-Host "   RDS Endpoint: $RdsEndpoint" -ForegroundColor Yellow
Write-Host "   Database: $RdsDatabase" -ForegroundColor Yellow
Write-Host "`n📝 Next steps:" -ForegroundColor Cyan
Write-Host "   1. Store connection string in AWS Secrets Manager"
Write-Host "   2. Update ECS task definition to use the secret"
Write-Host "   3. Test connection from your application"

# Remove password from environment
Remove-Item Env:\PGPASSWORD
