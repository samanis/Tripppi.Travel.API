namespace Tripppi.Travel.API.Services.DTOs
{
    /// <summary>
    /// Represents one flight leg (e.g., outbound or return journey).
    /// A leg is a complete journey from origin to destination, which may include connecting flights.
    /// </summary>
    public sealed record FlightLegResult
    {
        /// <summary>
        /// Origin airport IATA code.
        /// </summary>
        public required string OriginIataCode { get; init; }

        /// <summary>
        /// Origin airport name.
        /// </summary>
        public required string OriginAirportName { get; init; }

        /// <summary>
        /// Origin city name.
        /// </summary>
        public required string OriginCityName { get; init; }

        /// <summary>
        /// Destination airport IATA code.
        /// </summary>
        public required string DestinationIataCode { get; init; }

        /// <summary>
        /// Destination airport name.
        /// </summary>
        public required string DestinationAirportName { get; init; }

        /// <summary>
        /// Destination city name.
        /// </summary>
        public required string DestinationCityName { get; init; }

        /// <summary>
        /// Total travel duration for this leg (including layovers).
        /// </summary>
        public required TimeSpan Duration { get; init; }

        /// <summary>
        /// Individual flight segments within this leg.
        /// Direct flight = 1 segment, flight with layover = 2+ segments.
        /// </summary>
        public required List<FlightSegmentResult> Segments { get; init; }
    }
}
