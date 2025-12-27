using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Tripppi.Travel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IMemoryCache _cache;
        private readonly IConfiguration _configuration;
        private readonly ILogger<HealthController> _logger;

        public HealthController(IMemoryCache cache, IConfiguration configuration, ILogger<HealthController> logger)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public class HealthResponse
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = null!;

            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }

            [JsonPropertyName("cache")]
            public CacheStatus? Cache { get; set; }

            [JsonPropertyName("database")]
            public DatabaseStatus? Database { get; set; }
        }

        public class CacheStatus
        {
            [JsonPropertyName("enabled")]
            public bool Enabled { get; set; }

            [JsonPropertyName("airports_loaded")]
            public bool AirportsLoaded { get; set; }

            [JsonPropertyName("key")]
            public string? Key { get; set; }
        }

        public class DatabaseStatus
        {
            [JsonPropertyName("read_only")]
            public bool ReadOnly { get; set; }

            [JsonPropertyName("connection_string_masked")]
            public string? ConnectionStringMasked { get; set; }
        }

        /// <summary>
        /// Health check endpoint reporting app status, cache, and database configuration.
        /// </summary>
        [HttpGet]
        public ActionResult<HealthResponse> GetHealth()
        {
            var cacheKey = _configuration.GetValue<string>("Cache:AirportsKey") ?? "AirportsCache";
            var isAirportsLoaded = _cache.TryGetValue(cacheKey, out var _);

            var connStr = _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            var isReadOnly = connStr.Contains("default_transaction_read_only=on", StringComparison.OrdinalIgnoreCase);

            var maskedConnStr = MaskConnectionString(connStr);

            _logger.LogInformation("Health check: cache loaded = {IsLoaded}, db read-only = {IsReadOnly}", isAirportsLoaded, isReadOnly);

            var response = new HealthResponse
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                Cache = new CacheStatus
                {
                    Enabled = true,
                    AirportsLoaded = isAirportsLoaded,
                    Key = cacheKey
                },
                Database = new DatabaseStatus
                {
                    ReadOnly = isReadOnly,
                    ConnectionStringMasked = maskedConnStr
                }
            };

            return Ok(response);
        }

        /// <summary>
        /// Minimal liveness check for container orchestration.
        /// </summary>
        [HttpGet("live")]
        public IActionResult GetLiveness()
        {
            return Ok(new { status = "alive" });
        }

        /// <summary>
        /// Readiness check: confirms cache is loaded and app is ready to handle requests.
        /// </summary>
        [HttpGet("ready")]
        public IActionResult GetReadiness()
        {
            var cacheKey = _configuration.GetValue<string>("Cache:AirportsKey") ?? "AirportsCache";
            var isAirportsLoaded = _cache.TryGetValue(cacheKey, out var _);

            if (!isAirportsLoaded)
            {
                _logger.LogWarning("Readiness check failed: airports not loaded in cache");
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "not ready", reason = "airports cache not populated" });
            }

            return Ok(new { status = "ready" });
        }

        private static string MaskConnectionString(string connStr)
        {
            if (string.IsNullOrWhiteSpace(connStr))
                return "(empty)";

            // Mask password
            var masked = System.Text.RegularExpressions.Regex.Replace(
                connStr,
                @"Password=([^;]*)",
                "Password=***",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return masked;
        }
    }
}
