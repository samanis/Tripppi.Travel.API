using System.ComponentModel.DataAnnotations;

namespace Tripppi.Travel.API.Configuration
{
    /// <summary>
    /// Configuration options for Duffel API integration.
    /// Loaded from appsettings.json "Duffel" section.
    /// </summary>
    public sealed class DuffelOptions
    {
        public const string SectionName = "Duffel";

        /// <summary>
        /// Duffel API key.
        /// Test: duffel_test_xxx (sandbox, no real bookings)
        /// Live: duffel_live_xxx (production, real bookings and charges)
        /// </summary>
        [Required(ErrorMessage = "Duffel API key is required")]
        [RegularExpression(@"^duffel_(test|live)_[a-zA-Z0-9_-]+$", 
            ErrorMessage = "Invalid Duffel API key format")]
        public required string ApiKey { get; init; }

        /// <summary>
        /// Base URL for Duffel API (default: https://api.duffel.com).
        /// </summary>
        [Required(ErrorMessage = "Duffel base URL is required")]
        [Url(ErrorMessage = "Duffel base URL must be a valid URL")]
        public required string BaseUrl { get; init; }

        /// <summary>
        /// API version to use (default: v2).
        /// </summary>
        [Required]
        public string ApiVersion { get; init; } = "v2";

        /// <summary>
        /// Checks if this is a test/sandbox environment.
        /// </summary>
        public bool IsTestMode => ApiKey.StartsWith("duffel_test_", StringComparison.Ordinal);
    }
}
