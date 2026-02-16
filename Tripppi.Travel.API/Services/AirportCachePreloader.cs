using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tripppi.Travel.API.Infrastructure;
using Tripppi.Travel.API.Utilities;

namespace Tripppi.Travel.API.Services
{
    public sealed class AirportCacheOptions
    {
        public string AirportsKey { get; set; } = "AirportsCache";
        public int AirportsTtlSeconds { get; set; } = 86400;
    }

    public sealed class AirportCachePreloader : IHostedService
    {
        private readonly IServiceProvider _services;
        private readonly IMemoryCache _cache;
        private readonly AirportCacheOptions _options;
        private readonly ILogger<AirportCachePreloader> _logger;

        public AirportCachePreloader(IServiceProvider services, IMemoryCache cache, IOptions<AirportCacheOptions> options, ILogger<AirportCachePreloader> logger)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var configuration = scope.ServiceProvider.GetService<IConfiguration>();

            // Step 0: Seed popularity scores (adds column if needed and populates tier values)
                // DISABLED - seeding should be done via migrations, not on every startup
                // This was causing database lock issues when app received first request
                /*
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    _logger.LogInformation("Seeding airport popularity scores...");
                    await AirportPopularitySeeder.SeedAsync(db, _logger);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to seed popularity scores: {Message}", ex.Message);
                }
                */

            // Step 1: Best-effort: test DB connection and log result (do not log sensitive data)
            try
            {
                var connStr = configuration?.GetConnectionString("DefaultConnection");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    try
                    {
                        var csb = new Npgsql.NpgsqlConnectionStringBuilder(connStr);
                        var host = csb.Host;
                        var port = csb.Port;
                        var db = csb.Database;
                        var user = csb.Username;

                        _logger.LogInformation("Testing DB connection to {Host}:{Port} DB={Database} User={User}", host, port, db, user);

                        await using var testConn = new Npgsql.NpgsqlConnection(connStr);
                        await testConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                        await testConn.CloseAsync();

                        _logger.LogInformation("Database connectivity check succeeded");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Database connectivity check failed: {Message}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to perform database connectivity check: {Message}", ex.Message);
            }

            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var airports = await db.Airports
                    .AsNoTracking()
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.AirportsTtlSeconds)
                };

                _cache.Set(_options.AirportsKey, airports.AsReadOnly(), cacheEntryOptions);
                _logger.LogInformation("Loaded {Count} airports into memory cache", airports.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CRITICAL: Failed to preload airports into cache. App will not function correctly: {Message}", ex.Message);
                // Re-throw because app cannot function without airport cache
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
