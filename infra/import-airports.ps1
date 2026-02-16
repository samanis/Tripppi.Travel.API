$ErrorActionPreference = "Stop"

Write-Host "=== Importing Airports to AWS RDS ===" -ForegroundColor Cyan

# Load Npgsql
$dllPath = "C:\Users\saman\source\repos\Tripppi.Travel.API\Tripppi.Travel.API\bin\Debug\net10.0\Npgsql.dll"
Write-Host "Loading Npgsql from: $dllPath" -ForegroundColor Yellow
[Reflection.Assembly]::LoadFile($dllPath) | Out-Null

# Connect
$connStr = "Host=airpath-production-db.cl04ie2ior6k.ca-central-1.rds.amazonaws.com;Database=airpath;Username=airpathdb;Password=DqYsG0aRTg25pxo3IcBj;Timeout=60"
Write-Host "Connecting to RDS..." -ForegroundColor Yellow
$conn = New-Object Npgsql.NpgsqlConnection($connStr)
$conn.Open()
Write-Host "Connected!" -ForegroundColor Green

$cmd = $conn.CreateCommand()

# Drop and create table
Write-Host "Dropping old table..." -ForegroundColor Yellow
$cmd.CommandText = "DROP TABLE IF EXISTS airports"
$cmd.ExecuteNonQuery() | Out-Null

Write-Host "Creating table..." -ForegroundColor Yellow
$cmd.CommandText = "CREATE TABLE airports (airport_name TEXT, airport_iata TEXT, airport_icao TEXT, latitude DOUBLE PRECISION, longitude DOUBLE PRECISION, city_name TEXT, city_iata_code TEXT, city_icao_code TEXT, country_name TEXT, country_icao TEXT, country_iata TEXT, popularity_score INTEGER)"
$cmd.ExecuteNonQuery() | Out-Null

# Import data
$csvPath = "C:\Users\saman\source\repos\Tripppi.Travel.API\Tripppi.Travel.API\bin\data\major_airports_enriched.csv"
Write-Host "Loading CSV..." -ForegroundColor Yellow
$airports = Import-Csv $csvPath
Write-Host "Found $($airports.Count) airports" -ForegroundColor Cyan

Write-Host "Importing..." -ForegroundColor Yellow
$i = 0
foreach ($a in $airports) {
    $cmd.CommandText = "INSERT INTO airports VALUES (@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12)"
    $cmd.Parameters.Clear()
    
    $cmd.Parameters.AddWithValue("p1", $a.airport_name) | Out-Null
    $cmd.Parameters.AddWithValue("p2", $a.airport_iata) | Out-Null
    $cmd.Parameters.AddWithValue("p3", $a.airport_icao) | Out-Null
    $cmd.Parameters.AddWithValue("p4", [double]$a.latitude) | Out-Null
    $cmd.Parameters.AddWithValue("p5", [double]$a.longitude) | Out-Null
    $cmd.Parameters.AddWithValue("p6", $a.city_name) | Out-Null
    $cmd.Parameters.AddWithValue("p7", $a.city_iata_code) | Out-Null
    $cmd.Parameters.AddWithValue("p8", $a.city_icao_code) | Out-Null
    $cmd.Parameters.AddWithValue("p9", $a.country_name) | Out-Null
    $cmd.Parameters.AddWithValue("p10", $a.country_icao) | Out-Null
    $cmd.Parameters.AddWithValue("p11", $a.country_iata) | Out-Null
    
    $pop = if ($a.popularity_score -and $a.popularity_score -ne "") { [int]$a.popularity_score } else { 10 }
    $cmd.Parameters.AddWithValue("p12", $pop) | Out-Null
    
    $cmd.ExecuteNonQuery() | Out-Null
    $i++
    
    if ($i % 500 -eq 0) {
        Write-Host "  Imported $i..." -ForegroundColor Gray
    }
}

# Verify
$cmd.CommandText = "SELECT COUNT(*) FROM airports"
$count = $cmd.ExecuteScalar()

Write-Host ""
Write-Host "SUCCESS! Imported $count airports to AWS RDS" -ForegroundColor Green

$conn.Close()
