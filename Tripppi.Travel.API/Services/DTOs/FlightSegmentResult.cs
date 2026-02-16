namespace Tripppi.Travel.API.Services.DTOs
{
    /// <summary>
    /// Represents an individual flight segment (operated by a specific airline).
    /// A segment is a single flight operated by one aircraft.
    /// </summary>
    public sealed record FlightSegmentResult
    {
        /// <summary>
        /// Operating carrier IATA code (e.g., "AA" for American Airlines).
        /// </summary>
        public required string OperatingCarrierIataCode { get; init; }

        /// <summary>
        /// Operating carrier name.
        /// </summary>
        public required string OperatingCarrierName { get; init; }

        /// <summary>
        /// Marketing carrier IATA code (for codeshare flights).
        /// </summary>
        public required string MarketingCarrierIataCode { get; init; }

        /// <summary>
        /// Flight number (e.g., "1234").
        /// </summary>
        public required string FlightNumber { get; init; }

        /// <summary>
        /// Aircraft type (e.g., "Boeing 737-800").
        /// </summary>
        public string? AircraftName { get; init; }

        /// <summary>
        /// Origin airport IATA code.
        /// </summary>
        public required string OriginIataCode { get; init; }

        /// <summary>
        /// Origin terminal (if available).
        /// </summary>
        public string? OriginTerminal { get; init; }

        /// <summary>
        /// Destination airport IATA code.
        /// </summary>
        public required string DestinationIataCode { get; init; }

        /// <summary>
        /// Destination terminal (if available).
        /// </summary>
        public string? DestinationTerminal { get; init; }

        /// <summary>
        /// Departure time (local time at origin).
        /// </summary>
        public required DateTime DepartingAt { get; init; }

        /// <summary>
        /// Arrival time (local time at destination).
        /// </summary>
        public required DateTime ArrivingAt { get; init; }

        /// <summary>
        /// Flight duration (not including layover).
        /// </summary>
        public required TimeSpan Duration { get; init; }
    }
}
