# Import airports data to AWS RDS via SSH tunnel
# Prerequisites: SSH tunnel must be running on localhost:5433

Write-Host "=== AWS RDS Migration via SSH Tunnel ===" -ForegroundColor Cyan
Write-Host ""

$ErrorActionPreference = "Stop"

# Connection details (via tunnel)
$connString = "Host=localhost;Port=5433;Database=airpath;Username=airpathdb;Password=DqYsG0aRTg25pxo3IcBj;Timeout=60"
$csvPath = "C:\Users\saman\source\repos\Tripppi.Travel.API\Tripppi.Travel.API\bin\data\major_airports_enriched.csv"
$npgsqlPath = "C:\Users\saman\source\repos\Tripppi.Travel.API\Tripppi.Travel.API\bin\Debug\net10.0\Npgsql.dll"

# Load Npgsql
Write-Host "Loading Npgsql..." -ForegroundColor Yellow
[System.Reflection.Assembly]::LoadFrom($npgsqlPath) | Out-Null

# Connect to RDS
Write-Host "Connecting to AWS RDS via SSH tunnel..." -ForegroundColor Yellow
$conn = New-Object Npgsql.NpgsqlConnection($connString)
$conn.Open()
Write-Host "✓ Connected!" -ForegroundColor Green
Write-Host ""

try {
    # Drop table if exists
    Write-Host "Dropping existing table (if any)..." -ForegroundColor Yellow
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "DROP TABLE IF EXISTS airports"
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "✓ Done" -ForegroundColor Green
    
    # Create table
    Write-Host "Creating airports table..." -ForegroundColor Yellow
    $cmd.CommandText = @"
CREATE TABLE airports (
    airport_name TEXT,
    airport_iata TEXT,
    airport_icao TEXT,
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    city_name TEXT,
    city_iata_code TEXT,
    city_icao_code TEXT,
    country_name TEXT,
    country_icao TEXT,
    country_iata TEXT,
    popularity_score INTEGER
)
"@
    $cmd.ExecuteNonQuery() | Out-Null
    Write-Host "✓ Table created" -ForegroundColor Green
    Write-Host ""
    
    # Load CSV
    Write-Host "Loading CSV data..." -ForegroundColor Yellow
    if (-not (Test-Path $csvPath)) {
        throw "CSV file not found: $csvPath"
    }
    
    $airports = Import-Csv $csvPath
    Write-Host "✓ Loaded $($airports.Count) airports from CSV" -ForegroundColor Green
    Write-Host ""
    
    # Import data
    Write-Host "Importing airports to RDS..." -ForegroundColor Yellow
    $imported = 0
    $failed = 0
    
    foreach ($airport in $airports) {
        try {
            $cmd.CommandText = "INSERT INTO airports VALUES (@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12)"
            $cmd.Parameters.Clear()
            
            [void]$cmd.Parameters.AddWithValue("p1", $airport.airport_name)
            [void]$cmd.Parameters.AddWithValue("p2", $airport.airport_iata)
            [void]$cmd.Parameters.AddWithValue("p3", $airport.airport_icao)
            [void]$cmd.Parameters.AddWithValue("p4", [double]::Parse($airport.latitude))
            [void]$cmd.Parameters.AddWithValue("p5", [double]::Parse($airport.longitude))
            [void]$cmd.Parameters.AddWithValue("p6", $airport.city_name)
            [void]$cmd.Parameters.AddWithValue("p7", $airport.city_iata_code)
            [void]$cmd.Parameters.AddWithValue("p8", $airport.city_icao_code)
            [void]$cmd.Parameters.AddWithValue("p9", $airport.country_name)
            [void]$cmd.Parameters.AddWithValue("p10", $airport.country_icao)
            [void]$cmd.Parameters.AddWithValue("p11", $airport.country_iata)
            
            # Handle empty popularity_score
            if ([string]::IsNullOrWhiteSpace($airport.popularity_score)) {
                [void]$cmd.Parameters.AddWithValue("p12", 10)  # default
            } else {
                [void]$cmd.Parameters.AddWithValue("p12", [int]::Parse($airport.popularity_score))
            }
            
            $cmd.ExecuteNonQuery() | Out-Null
            $imported++
            
            if ($imported % 1000 -eq 0) {
                Write-Host "  Imported $imported airports..." -ForegroundColor Gray
            }
        } catch {
            $failed++
            Write-Host "  Warning: Failed to import $($airport.airport_iata): $_" -ForegroundColor Red
        }
    }
    
    Write-Host "✓ Import complete" -ForegroundColor Green
    Write-Host "  - Successful: $imported" -ForegroundColor Green
    Write-Host "  - Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Yellow" } else { "Green" })
    Write-Host ""
    
    # Verify
    Write-Host "Verifying data..." -ForegroundColor Yellow
    $cmd.CommandText = "SELECT COUNT(*) FROM airports"
    $count = $cmd.ExecuteScalar()
    Write-Host "✓ Total airports in RDS: $count" -ForegroundColor Green
    Write-Host ""
    
    # Sample query
    Write-Host "Sample Toronto airports:" -ForegroundColor Yellow
    $cmd.CommandText = "SELECT airport_iata, airport_name, city_name FROM airports WHERE city_name ILIKE '%toronto%' LIMIT 3"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        Write-Host "  $($reader[0]) - $($reader[1]), $($reader[2])" -ForegroundColor Cyan
    }
    $reader.Close()
    
    Write-Host ""
    Write-Host "=== Migration Successful! ===" -ForegroundColor Green
    
} finally {
    $conn.Close()
}
