using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Collections.Generic;
using Tripppi.Travel.API.Infrastructure;

namespace Tripppi.Travel.API.Utilities
{
    /// <summary>
    /// Utility to seed airport popularity scores on application startup.
    /// Handles automatic column creation and populates tier-based scores.
    /// Uses a dedicated write-enabled connection for seeding (bypassing read-only session mode).
    /// All IATA codes and table names are hardcoded (no user input), so string interpolation is safe.
    /// </summary>
    [Obsolete("Do not use. Seeding should be done via database migrations, not on startup. Kept for reference only.")]
    public sealed class AirportPopularitySeeder
    {
        private static readonly Dictionary<string, int> PopularityTiers = new()
        {
            // TIER 1: Global Super Hubs (Score: 1000)
            { "JFK", 1000 }, { "LAX", 1000 }, { "ORD", 1000 }, { "DFW", 1000 }, { "DEN", 1000 },
            { "ATL", 1000 }, { "LHR", 1000 }, { "CDG", 1000 }, { "AMS", 1000 }, { "FRA", 1000 },
            { "IST", 1000 }, { "DXB", 1000 }, { "SIN", 1000 }, { "HND", 1000 }, { "NRT", 1000 },
            { "ICN", 1000 }, { "PVG", 1000 }, { "HKG", 1000 }, { "SYD", 1000 },

            // TIER 2: Major Regional Hubs (Score: 850)
            { "YYZ", 850 }, { "YVR", 850 }, { "MEX", 850 }, { "SFO", 850 }, { "SEA", 850 },
            { "MIA", 850 }, { "BOS", 850 }, { "LGA", 850 }, { "LIH", 850 }, { "PHX", 850 },
            { "FCO", 850 }, { "MAD", 850 }, { "BCN", 850 }, { "DUB", 850 }, { "LGW", 850 },
            { "MAN", 850 }, { "ORY", 850 }, { "BVA", 850 }, { "KIX", 850 }, { "PEK", 850 },

            // TIER 3: Large Regional Airports (Score: 600)
            { "YUL", 600 }, { "YEG", 600 }, { "YYC", 600 }, { "YOW", 600 }, { "YTZ", 600 },
            { "BNA", 600 }, { "IAD", 600 }, { "DCA", 600 }, { "PHL", 600 }, { "TPA", 600 },
            { "FLL", 600 }, { "MCO", 600 }, { "AUS", 600 }, { "IAH", 600 }, { "SAT", 600 },
            { "SAN", 600 }, { "LAS", 600 }, { "MSP", 600 }, { "STL", 600 }, { "DTW", 600 },
            { "PIT", 600 }, { "CVG", 600 }, { "EWR", 600 }, { "MSY", 600 }, { "SLC", 600 },

            // TIER 4: Regional Airports (Score: 300)
            { "YKA", 300 }, { "YLD", 300 }, { "TYS", 300 }, { "GRR", 300 }, { "CLE", 300 },
            { "IND", 300 }, { "MCI", 300 }, { "TUS", 300 }, { "ABQ", 300 }, { "SMF", 300 },
            { "SJC", 300 }, { "OAK", 300 }, { "EUG", 300 }, { "PDX", 300 }, { "OLM", 300 },
            { "KTN", 300 }, { "JNU", 300 }, { "ANC", 300 }, { "FAI", 300 }, { "PWM", 300 },
            { "BTV", 300 },

            // TIER 5: Small Regional/Local (Score: 100)
            { "YJA", 100 }, { "YPA", 100 }, { "YEW", 100 }, { "YLB", 100 }, { "YFB", 100 },
            { "ACV", 100 }, { "EYW", 100 }
        };

        public static async Task SeedAsync(AppDbContext db, ILogger? logger = null)
        {
            try
            {
                // Get the raw connection string and strip the read-only option for seeding
                var connString = db.Database.GetConnectionString();
                if (string.IsNullOrEmpty(connString))
                {
                    logger?.LogWarning("[AirportPopularitySeeder] No connection string found; skipping seeding.");
                    return;
                }

                // Remove read-only mode from connection string for this operation
                var seedingConnString = RemoveReadOnlyOption(connString);

                // Step 1: Check if column exists
                var columnExists = await ColumnExistsAsync(seedingConnString);
                
                if (!columnExists)
                {
                    logger?.LogInformation("[AirportPopularitySeeder] Adding popularity_score column...");
                    await ExecuteSqlRawAsync(seedingConnString,
                        "ALTER TABLE airports ADD COLUMN popularity_score INTEGER DEFAULT 10");
                    logger?.LogInformation("[AirportPopularitySeeder] Column added successfully.");
                }

                // Step 2: Seed tier values by grouping airports by score
                logger?.LogInformation("[AirportPopularitySeeder] Seeding popularity scores...");

                var scoreGroups = PopularityTiers
                    .GroupBy(x => x.Value)
                    .OrderByDescending(x => x.Key)
                    .ToList();

                foreach (var scoreGroup in scoreGroups)
                {
                    var score = scoreGroup.Key;
                    var iataList = scoreGroup.Select(x => x.Key).ToArray();
                    var iataValues = string.Join(",", iataList.Select(x => $"'{x}'"));

                    // All IATA codes are hardcoded from the tier dictionary (not user input), so string interpolation is safe
                    await ExecuteSqlRawAsync(seedingConnString,
                        $"UPDATE airports SET popularity_score = {score} WHERE airport_iata IN ({iataValues})");
                }

                logger?.LogInformation("[AirportPopularitySeeder] Popularity scores seeded successfully.");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[AirportPopularitySeeder] Error during seeding: {Message}", ex.Message);
                // Don't throw; allow app to continue even if seeding fails
            }
        }

        private static string RemoveReadOnlyOption(string connectionString)
        {
            // Parse and rebuild connection string without read-only options
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                
                // Remove Options that contain read-only flag
                if (!string.IsNullOrEmpty(builder.Options))
                {
                    var options = builder.Options.Split(';');
                    var filtered = options
                        .Where(o => !o.Contains("default_transaction_read_only", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    builder.Options = string.Join(";", filtered);
                }

                return builder.ConnectionString;
            }
            catch
            {
                return connectionString;
            }
        }

        private static async Task ExecuteSqlRawAsync(string connString, string sql)
        {
            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task<bool> ColumnExistsAsync(string connString)
        {
            try
            {
                await using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1 FROM information_schema.columns WHERE table_name = 'airports' AND column_name = 'popularity_score'";
                var result = await cmd.ExecuteScalarAsync();
                return result != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
