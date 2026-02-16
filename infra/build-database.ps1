#!/usr/bin/env pwsh
# Database Build Script - Create and populate Airpath database
# Can be used for local development or AWS RDS setup

param(
    [Parameter(Mandatory=$true)]
    [string]$Host,
    
    [Parameter(Mandatory=$true)]
    [string]$Username,
    
    [Parameter(Mandatory=$true)]
    [string]$Password,
    
    [string]$Database = "airpath",
    [string]$Port = "5432",
    [string]$CsvPath = "../Tripppi.Travel.API/bin/data/major_airports_enriched.csv",
    [switch]$DropExisting = $false
)

$ErrorActionPreference = "Stop"

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║         Airpath Database Build Script                     ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Set environment variable for psql password
$env:PGPASSWORD = $Password

$ConnectionArgs = "-h $Host -p $Port -U $Username"

Write-Host "📋 Configuration:" -ForegroundColor Yellow
Write-Host "   Host: $Host" -ForegroundColor Gray
Write-Host "   Port: $Port" -ForegroundColor Gray
Write-Host "   Database: $Database" -ForegroundColor Gray
Write-Host "   Username: $Username" -ForegroundColor Gray
Write-Host ""

# Step 1: Check if database exists
Write-Host "🔍 Step 1: Checking database existence..." -ForegroundColor Cyan

$DbExistsQuery = "SELECT 1 FROM pg_database WHERE datname = '$Database';"
$DbExists = psql $ConnectionArgs -d postgres -t -c $DbExistsQuery 2>$null

if ($DbExists -and $DropExisting) {
    Write-Host "   ⚠️  Database exists. Dropping and recreating..." -ForegroundColor Yellow
    
    # Terminate existing connections
    $TerminateQuery = @"
SELECT pg_terminate_backend(pg_stat_activity.pid)
FROM pg_stat_activity
WHERE pg_stat_activity.datname = '$Database'
  AND pid <> pg_backend_pid();
"@
    psql $ConnectionArgs -d postgres -c $TerminateQuery | Out-Null
    
    # Drop database
    psql $ConnectionArgs -d postgres -c "DROP DATABASE IF EXISTS $Database;" | Out-Null
    Write-Host "   ✅ Old database dropped" -ForegroundColor Green
}

if (-not $DbExists -or $DropExisting) {
    Write-Host "   📦 Creating database '$Database'..." -ForegroundColor Cyan
    psql $ConnectionArgs -d postgres -c "CREATE DATABASE $Database ENCODING 'UTF8';" | Out-Null
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✅ Database created successfully" -ForegroundColor Green
    } else {
        Write-Host "   ❌ Failed to create database" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "   ✅ Database already exists (use -DropExisting to recreate)" -ForegroundColor Green
}

Write-Host ""

# Step 2: Create schema
Write-Host "🏗️  Step 2: Creating database schema..." -ForegroundColor Cyan

$SchemaSql = @"
-- Drop existing tables (if recreating schema)
DROP TABLE IF EXISTS airports CASCADE;

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
    popularity_score INTEGER DEFAULT 10
);

-- Create indexes for query performance
CREATE INDEX idx_airports_iata ON airports(airport_iata) WHERE airport_iata IS NOT NULL;
CREATE INDEX idx_airports_icao ON airports(airport_icao) WHERE airport_icao IS NOT NULL;
CREATE INDEX idx_airports_name ON airports USING gin(to_tsvector('english', airport_name));
CREATE INDEX idx_airports_city ON airports USING gin(to_tsvector('english', city_name));
CREATE INDEX idx_airports_country ON airports(country_name);
CREATE INDEX idx_airports_popularity ON airports(popularity_score DESC NULLS LAST);

-- Create composite index for common queries
CREATE INDEX idx_airports_search ON airports(popularity_score DESC, airport_name, city_name);

-- Add comments for documentation
COMMENT ON TABLE airports IS 'Global airport directory with IATA/ICAO codes and location data';
COMMENT ON COLUMN airports.popularity_score IS 'Higher score = more popular airport (used for search ranking)';
"@

$TempSchemaFile = [System.IO.Path]::GetTempFileName() + ".sql"
$SchemaSql | Out-File -FilePath $TempSchemaFile -Encoding UTF8

psql $ConnectionArgs -d $Database -f $TempSchemaFile -q

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ Schema created successfully" -ForegroundColor Green
} else {
    Write-Host "   ❌ Schema creation failed" -ForegroundColor Red
    Remove-Item $TempSchemaFile -ErrorAction SilentlyContinue
    exit 1
}

Remove-Item $TempSchemaFile -ErrorAction SilentlyContinue
Write-Host ""

# Step 3: Load data from CSV
Write-Host "📊 Step 3: Loading airport data from CSV..." -ForegroundColor Cyan

if (-not (Test-Path $CsvPath)) {
    Write-Host "   ❌ CSV file not found: $CsvPath" -ForegroundColor Red
    Write-Host "   💡 Build the project first: dotnet build" -ForegroundColor Yellow
    exit 1
}

$AbsoluteCsvPath = (Resolve-Path $CsvPath).Path.Replace('\', '/')

$CopySql = @"
\COPY airports(airport_name, airport_iata, airport_icao, latitude, longitude, city_name, city_iata_code, city_icao_code, country_name, country_icao, country_iata, popularity_score) FROM '$AbsoluteCsvPath' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8');
"@

$TempCopyFile = [System.IO.Path]::GetTempFileName() + ".sql"
$CopySql | Out-File -FilePath $TempCopyFile -Encoding UTF8

psql $ConnectionArgs -d $Database -f $TempCopyFile -q

if ($LASTEXITCODE -eq 0) {
    Write-Host "   ✅ Data loaded successfully" -ForegroundColor Green
} else {
    Write-Host "   ❌ Data import failed" -ForegroundColor Red
    Remove-Item $TempCopyFile -ErrorAction SilentlyContinue
    exit 1
}

Remove-Item $TempCopyFile -ErrorAction SilentlyContinue
Write-Host ""

# Step 4: Verify data
Write-Host "🔍 Step 4: Verifying database..." -ForegroundColor Cyan

$CountQuery = "SELECT COUNT(*) as total_airports FROM airports;"
$TopQuery = @"
SELECT 
    airport_iata as IATA, 
    airport_name as Airport, 
    city_name as City, 
    country_name as Country,
    popularity_score as Pop
FROM airports 
WHERE airport_iata IS NOT NULL
ORDER BY popularity_score DESC 
LIMIT 10;
"@

Write-Host "   Total airports:" -ForegroundColor Gray
psql $ConnectionArgs -d $Database -c $CountQuery

Write-Host ""
Write-Host "   Top 10 popular airports:" -ForegroundColor Gray
psql $ConnectionArgs -d $Database -c $TopQuery

Write-Host ""

# Step 5: Vacuum and analyze for performance
Write-Host "⚡ Step 5: Optimizing database..." -ForegroundColor Cyan
psql $ConnectionArgs -d $Database -c "VACUUM ANALYZE airports;" -q
Write-Host "   ✅ Optimization complete" -ForegroundColor Green

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║         Database build completed successfully! ✅          ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "📝 Connection string for appsettings.json:" -ForegroundColor Yellow
Write-Host "Host=$Host;Port=$Port;Database=$Database;Username=$Username;Password=YOUR_PASSWORD;SSL Mode=Require" -ForegroundColor Gray
Write-Host ""
Write-Host "🧪 Test with a search query:" -ForegroundColor Yellow
Write-Host "SELECT * FROM airports WHERE airport_iata = 'YYZ' OR city_name ILIKE '%toronto%';" -ForegroundColor Gray
Write-Host ""

# Clean up password from environment
Remove-Item Env:\PGPASSWORD
