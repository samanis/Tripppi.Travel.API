using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Tripppi.Travel.API.Domain;
using Tripppi.Travel.API.Infrastructure;

namespace Tripppi.Travel.API.Repositories
{
    /// <summary>
    /// Read-only EF Core repository for <see cref="Airport"/>.
    /// Uses AsNoTracking for all queries because the data is effectively immutable for the application.
    /// </summary>
    public sealed class EfAirportRepository : IAirportRepository
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        private readonly string _cacheKey;
        private readonly ILogger<EfAirportRepository> _logger;

        public EfAirportRepository(AppDbContext db, IMemoryCache cache, IConfiguration config, ILogger<EfAirportRepository> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _cacheKey = config.GetValue<string>("Cache:AirportsKey") ?? "AirportsCache";
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Searches airports with relevance scoring across all fields.
        /// Scoring done in C# memory after loading data. Returns top 5 results.
        /// Scores: Exact IATA (1000) > IATA prefix (800) > Airport prefix (600) > City IATA prefix (550) > 
        /// City prefix (500) > Country prefix (400) > Airport contains (300) > City contains (200) > Country contains (100)
        /// </summary>
        public async Task<IReadOnlyList<Airport>> SearchWithScoringAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
                return Array.Empty<Airport>();

            query = query.Trim();
            var queryLower = query.ToLowerInvariant();

            // Try cache first
            if (_cache.TryGetValue<IReadOnlyList<Airport>>(_cacheKey, out var cached) && cached?.Count > 0)
            {
                var resultsFromCache = cached
                    .Select(a => new { Airport = a, Score = CalculateScore(a, queryLower) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenBy(x => x.Airport.AirportName)
                    .Take(5)
                    .Select(x => x.Airport)
                    .ToList();

                return resultsFromCache;
            }

            // Load all airports into memory
            var allAirports = await _db.Airports
                .AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            // populate cache as best-effort
            try
            {
                _cache.Set(_cacheKey, allAirports.AsReadOnly());
                // Log cache size and estimated memory usage
                var count = allAirports.Count;
                // Estimate: each Airport ~350 bytes (very rough, depends on string sizes)
                var estimatedBytes = count * 350;
                var estimatedMB = estimatedBytes / (1024.0 * 1024.0);
                _logger.LogInformation($"[EfAirportRepository] Cached {count} airports (~{estimatedMB:F2} MB estimated)");
            }
            catch { }

            // Score and rank in C#
            var results = allAirports
                .Select(a => new
                {
                    Airport = a,
                    Score = CalculateScore(a, queryLower)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Airport.AirportName)
                .Take(5)
                .Select(x => x.Airport)
                .ToList();

            return results;
        }

        private int CalculateScore(Airport airport, string queryLower)
        {
            int textScore = 0;

            // Exact IATA match (highest priority)
            if (!string.IsNullOrEmpty(airport.AirportIata) && 
                airport.AirportIata.Equals(queryLower, StringComparison.OrdinalIgnoreCase))
                textScore += 1000;

            // IATA starts with query
            if (!string.IsNullOrEmpty(airport.AirportIata) && 
                airport.AirportIata.StartsWith(queryLower, StringComparison.OrdinalIgnoreCase))
                textScore += 800;

            // Airport name starts with query
            if (!string.IsNullOrEmpty(airport.AirportName) && 
                airport.AirportName.StartsWith(queryLower, StringComparison.OrdinalIgnoreCase))
                textScore += 600;

            // City IATA starts with query
            if (!string.IsNullOrEmpty(airport.CityIataCode) && 
                airport.CityIataCode.StartsWith(queryLower, StringComparison.OrdinalIgnoreCase))
                textScore += 550;

            // City name starts with query
            if (!string.IsNullOrEmpty(airport.CityName) && 
                airport.CityName.StartsWith(queryLower, StringComparison.OrdinalIgnoreCase))
                textScore += 500;

            // Country IATA starts with query
            if (!string.IsNullOrEmpty(airport.CountryIata) && 
                airport.CountryIata.StartsWith(queryLower, StringComparison.OrdinalIgnoreCase))
                textScore += 450;

            // Country name starts with query
            if (!string.IsNullOrEmpty(airport.CountryName) && 
                airport.CountryName.StartsWith(queryLower, StringComparison.OrdinalIgnoreCase))
                textScore += 400;

            // Only use "contains" matching for longer queries (5+ characters)
            // This prevents popular airports from appearing in short queries where they're not relevant
            if (queryLower.Length >= 5)
            {
                // Airport name contains query (much lower weight to favor exact/prefix matches)
                if (!string.IsNullOrEmpty(airport.AirportName) && 
                    airport.AirportName.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                    textScore += 50;

                // City name contains query (much lower weight)
                if (!string.IsNullOrEmpty(airport.CityName) && 
                    airport.CityName.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                    textScore += 30;

                // Country name contains query (much lower weight)
                if (!string.IsNullOrEmpty(airport.CountryName) && 
                    airport.CountryName.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                    textScore += 20;
            }

            // If no text match at all, return 0 (don't rank on popularity alone)
            if (textScore == 0)
                return 0;

            // Combine text match (70%) + popularity (30%) - increased text relevance weight
            // Default popularity to 10 if not set
            var popularityScore = airport.PopularityScore ?? 10;
            
            // Normalize text score to 0-100 range (max text score is 1000)
            var normalizedTextScore = Math.Min(textScore / 10, 100);
            
            // Normalize popularity to 0-100 range (max popularity is 1000)
            var normalizedPopularity = Math.Min(popularityScore / 10, 100);
            
            // Final score: 70% text relevance + 30% popularity (favors exact matches more)
            var finalScore = (int)((normalizedTextScore * 0.7) + (normalizedPopularity * 0.3));

            return finalScore;
        }
    }
}