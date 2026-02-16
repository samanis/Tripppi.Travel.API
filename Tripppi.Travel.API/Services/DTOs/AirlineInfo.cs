namespace Tripppi.Travel.API.Services.DTOs
{
    /// <summary>
    /// Airline information.
    /// </summary>
    public sealed record AirlineInfo
    {
        /// <summary>
        /// Airline IATA code (e.g., "AA").
        /// </summary>
        public required string IataCode { get; init; }

        /// <summary>
        /// Airline full name (e.g., "American Airlines").
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// URL to airline logo image.
        /// </summary>
        public string? LogoUrl { get; init; }
    }
}
