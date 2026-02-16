using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Tripppi.Travel.API.Infrastructure;

namespace Tripppi.Travel.API.Services
{
    public sealed class PopularityScoreSeeder
    {
        private static readonly Dictionary<string, int> AirportPopularityTiers = new()
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

        public static async Task SeedPopularityScoresAsync(AppDbContext db, ILogger? logger = null)
        {
            try
            {
                // Check if any airports already have popularity scores
                var airportsWithScore = await db.Airports
                    .AsNoTracking()
                    .Where(a => a.PopularityScore != null && a.PopularityScore > 0)
                    .CountAsync();

                if (airportsWithScore > 0)
                {
                    // Popularity scores already seeded
                    return;
                }

                // This is a read-only seeder; in production, run the SQL migration separately.
                // This method logs that the migration is needed.
                logger?.LogWarning("[PopularityScoreSeeder] Database migration needed. Please run:");
                logger?.LogWarning("[PopularityScoreSeeder] migration_001_add_popularity_score.sql");
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "[PopularityScoreSeeder] Error checking popularity scores: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Gets the popularity score for a given IATA code.
        /// Returns 10 (default) if not found in tier mapping.
        /// </summary>
        public static int GetPopularityScore(string? iata)
        {
            if (string.IsNullOrWhiteSpace(iata))
                return 10;

            return AirportPopularityTiers.TryGetValue(iata.ToUpper(), out var score) ? score : 10;
        }
    }
}
