#!/bin/bash
# Migrate Airport Data to AWS RDS PostgreSQL

set -e

# Configuration
RDS_ENDPOINT="${1}"
RDS_USERNAME="${2}"
RDS_PASSWORD="${3}"
RDS_DATABASE="${4:-airpath}"
CSV_PATH="Tripppi.Travel.API/bin/data/major_airports_enriched.csv"

if [ -z "$RDS_ENDPOINT" ] || [ -z "$RDS_USERNAME" ] || [ -z "$RDS_PASSWORD" ]; then
    echo "Usage: $0 <RDS_ENDPOINT> <RDS_USERNAME> <RDS_PASSWORD> [RDS_DATABASE]"
    echo "Example: $0 mydb.123456.us-east-1.rds.amazonaws.com admin MyPassword123 airpath"
    exit 1
fi

echo "🚀 Starting airport data migration to AWS RDS..."

# Export password for psql
export PGPASSWORD="$RDS_PASSWORD"

echo "📋 Step 1: Creating database schema..."

# Create schema
psql -h "$RDS_ENDPOINT" -U "$RDS_USERNAME" -d "$RDS_DATABASE" <<EOF
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
EOF

echo "✅ Schema created successfully"

echo "📊 Step 2: Loading airport data from CSV..."

# Import CSV data
psql -h "$RDS_ENDPOINT" -U "$RDS_USERNAME" -d "$RDS_DATABASE" <<EOF
\COPY airports(airport_name, airport_iata, airport_icao, latitude, longitude, city_name, city_iata_code, city_icao_code, country_name, country_icao, country_iata, popularity_score) FROM '$CSV_PATH' WITH (FORMAT csv, HEADER true);
EOF

echo "✅ Data imported successfully"

echo "🔍 Step 3: Verifying data..."

# Verify row count
psql -h "$RDS_ENDPOINT" -U "$RDS_USERNAME" -d "$RDS_DATABASE" -c "SELECT COUNT(*) as row_count FROM airports;"

# Sample data
echo ""
echo "Sample records:"
psql -h "$RDS_ENDPOINT" -U "$RDS_USERNAME" -d "$RDS_DATABASE" -c "SELECT airport_name, airport_iata, city_name, popularity_score FROM airports ORDER BY popularity_score DESC LIMIT 5;"

# Clean up
unset PGPASSWORD

echo ""
echo "✅ Migration completed successfully!"
echo "   RDS Endpoint: $RDS_ENDPOINT"
echo "   Database: $RDS_DATABASE"
echo ""
echo "📝 Next steps:"
echo "   1. Store connection string in AWS Secrets Manager"
echo "   2. Update ECS task definition to use the secret"
echo "   3. Test connection from your application"
