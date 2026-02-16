#!/bin/bash
# Database Build Script - Create and populate Airpath database
# Can be used for local development or AWS RDS setup

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

# Parse arguments
DB_HOST=""
DB_USERNAME=""
DB_PASSWORD=""
DB_NAME="airpath"
DB_PORT="5432"
CSV_PATH="../Tripppi.Travel.API/bin/data/major_airports_enriched.csv"
DROP_EXISTING=false

usage() {
    echo "Usage: $0 -h HOST -u USERNAME -p PASSWORD [-d DATABASE] [-P PORT] [-c CSV_PATH] [--drop]"
    echo ""
    echo "Options:"
    echo "  -h HOST         Database host"
    echo "  -u USERNAME     Database username"
    echo "  -p PASSWORD     Database password"
    echo "  -d DATABASE     Database name (default: airpath)"
    echo "  -P PORT         Database port (default: 5432)"
    echo "  -c CSV_PATH     Path to CSV file (default: ../Tripppi.Travel.API/bin/data/major_airports_enriched.csv)"
    echo "  --drop          Drop existing database before creating"
    exit 1
}

while [[ $# -gt 0 ]]; do
    case $1 in
        -h) DB_HOST="$2"; shift 2 ;;
        -u) DB_USERNAME="$2"; shift 2 ;;
        -p) DB_PASSWORD="$2"; shift 2 ;;
        -d) DB_NAME="$2"; shift 2 ;;
        -P) DB_PORT="$2"; shift 2 ;;
        -c) CSV_PATH="$2"; shift 2 ;;
        --drop) DROP_EXISTING=true; shift ;;
        *) usage ;;
    esac
done

if [ -z "$DB_HOST" ] || [ -z "$DB_USERNAME" ] || [ -z "$DB_PASSWORD" ]; then
    usage
fi

export PGPASSWORD="$DB_PASSWORD"

echo -e "${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║         Airpath Database Build Script                     ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

echo -e "${YELLOW}📋 Configuration:${NC}"
echo -e "${GRAY}   Host: $DB_HOST${NC}"
echo -e "${GRAY}   Port: $DB_PORT${NC}"
echo -e "${GRAY}   Database: $DB_NAME${NC}"
echo -e "${GRAY}   Username: $DB_USERNAME${NC}"
echo ""

# Step 1: Check if database exists
echo -e "${CYAN}🔍 Step 1: Checking database existence...${NC}"

DB_EXISTS=$(psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d postgres -t -c "SELECT 1 FROM pg_database WHERE datname = '$DB_NAME';" 2>/dev/null | xargs)

if [ "$DB_EXISTS" = "1" ] && [ "$DROP_EXISTING" = true ]; then
    echo -e "${YELLOW}   ⚠️  Database exists. Dropping and recreating...${NC}"
    
    # Terminate existing connections
    psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d postgres -c "SELECT pg_terminate_backend(pg_stat_activity.pid) FROM pg_stat_activity WHERE pg_stat_activity.datname = '$DB_NAME' AND pid <> pg_backend_pid();" >/dev/null 2>&1
    
    # Drop database
    psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d postgres -c "DROP DATABASE IF EXISTS $DB_NAME;" >/dev/null
    echo -e "${GREEN}   ✅ Old database dropped${NC}"
fi

if [ "$DB_EXISTS" != "1" ] || [ "$DROP_EXISTING" = true ]; then
    echo -e "${CYAN}   📦 Creating database '$DB_NAME'...${NC}"
    psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d postgres -c "CREATE DATABASE $DB_NAME ENCODING 'UTF8';" >/dev/null
    echo -e "${GREEN}   ✅ Database created successfully${NC}"
else
    echo -e "${GREEN}   ✅ Database already exists (use --drop to recreate)${NC}"
fi

echo ""

# Step 2: Create schema
echo -e "${CYAN}🏗️  Step 2: Creating database schema...${NC}"

psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d "$DB_NAME" -q <<EOF
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
    country_iata VARCHAR(11),
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
EOF

echo -e "${GREEN}   ✅ Schema created successfully${NC}"
echo ""

# Step 3: Load data from CSV
echo -e "${CYAN}📊 Step 3: Loading airport data from CSV...${NC}"

if [ ! -f "$CSV_PATH" ]; then
    echo -e "${RED}   ❌ CSV file not found: $CSV_PATH${NC}"
    echo -e "${YELLOW}   💡 Build the project first: dotnet build${NC}"
    exit 1
fi

ABS_CSV_PATH=$(realpath "$CSV_PATH")

psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d "$DB_NAME" -q <<EOF
\COPY airports(airport_name, airport_iata, airport_icao, latitude, longitude, city_name, city_iata_code, city_icao_code, country_name, country_icao, country_iata, popularity_score) FROM '$ABS_CSV_PATH' WITH (FORMAT csv, HEADER true, ENCODING 'UTF8');
EOF

echo -e "${GREEN}   ✅ Data loaded successfully${NC}"
echo ""

# Step 4: Verify data
echo -e "${CYAN}🔍 Step 4: Verifying database...${NC}"

echo -e "${GRAY}   Total airports:${NC}"
psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d "$DB_NAME" -c "SELECT COUNT(*) as total_airports FROM airports;"

echo ""
echo -e "${GRAY}   Top 10 popular airports:${NC}"
psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d "$DB_NAME" -c "SELECT airport_iata as IATA, airport_name as Airport, city_name as City, country_name as Country, popularity_score as Pop FROM airports WHERE airport_iata IS NOT NULL ORDER BY popularity_score DESC LIMIT 10;"

echo ""

# Step 5: Vacuum and analyze for performance
echo -e "${CYAN}⚡ Step 5: Optimizing database...${NC}"
psql -h "$DB_HOST" -p "$DB_PORT" -U "$DB_USERNAME" -d "$DB_NAME" -c "VACUUM ANALYZE airports;" -q
echo -e "${GREEN}   ✅ Optimization complete${NC}"

echo ""
echo -e "${GREEN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║         Database build completed successfully! ✅          ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${YELLOW}📝 Connection string for appsettings.json:${NC}"
echo -e "${GRAY}Host=$DB_HOST;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USERNAME;Password=YOUR_PASSWORD;SSL Mode=Require${NC}"
echo ""
echo -e "${YELLOW}🧪 Test with a search query:${NC}"
echo -e "${GRAY}SELECT * FROM airports WHERE airport_iata = 'YYZ' OR city_name ILIKE '%toronto%';${NC}"
echo ""

unset PGPASSWORD
