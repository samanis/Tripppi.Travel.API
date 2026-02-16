using System.ComponentModel.DataAnnotations;

namespace Tripppi.Travel.API.Services.DTOs
{
    /// <summary>
    /// Request DTO for flight search operations.
    /// Vendor-neutral model that can be mapped to any provider-specific format (Duffel, Amadeus, etc.).
    /// </summary>
    public sealed record FlightSearchRequest
    {
        /// <summary>
        /// Flight legs (one-way = 1 leg, round-trip = 2 legs).
        /// Each leg represents a journey from origin to destination.
        /// </summary>
        [Required, MinLength(1, ErrorMessage = "At least one leg is required")]
        public required List<FlightLegRequest> Legs { get; init; }

        /// <summary>
        /// Passengers traveling on this booking.
        /// </summary>
        [Required, MinLength(1, ErrorMessage = "At least one passenger is required")]
        public required List<PassengerRequest> Passengers { get; init; }

        /// <summary>
        /// Preferred cabin class for all segments.
        /// </summary>
        [Required]
        public required CabinClass CabinClass { get; init; }

        /// <summary>
        /// Maximum number of connections/stops allowed (null = no limit).
        /// 0 = direct flights only, 1 = max 1 stop, etc.
        /// </summary>
        [Range(0, 5, ErrorMessage = "Max connections must be between 0 and 5")]
        public int? MaxConnections { get; init; }

        /// <summary>
        /// Whether to return all offers at once (true) or stream results (false).
        /// Default: true for better UX.
        /// </summary>
        public bool ReturnAllOffers { get; init; } = true;

        /// <summary>
        /// Airlines to exclude from search results (IATA codes).
        /// </summary>
        public List<string>? ExcludedAirlines { get; init; }

        /// <summary>
        /// Corporate booking codes for discounted fares (if applicable).
        /// </summary>
        public List<string>? CorporateCodes { get; init; }
    }

    /// <summary>
    /// Represents a single flight leg (e.g., outbound or return journey).
    /// A leg is a journey from point A to point B, which may include one or more connecting flights.
    /// </summary>
    public sealed record FlightLegRequest
    {
        /// <summary>
        /// Origin airport IATA code (e.g., "YYZ").
        /// </summary>
        [Required, StringLength(3, MinimumLength = 3, ErrorMessage = "IATA code must be exactly 3 characters")]
        [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "IATA code must be 3 uppercase letters")]
        public required string OriginIataCode { get; init; }

        /// <summary>
        /// Destination airport IATA code (e.g., "JFK").
        /// </summary>
        [Required, StringLength(3, MinimumLength = 3, ErrorMessage = "IATA code must be exactly 3 characters")]
        [RegularExpression(@"^[A-Z]{3}$", ErrorMessage = "IATA code must be 3 uppercase letters")]
        public required string DestinationIataCode { get; init; }

        /// <summary>
        /// Departure date (local time at origin).
        /// </summary>
        [Required]
        [DataType(DataType.Date)]
        public required DateOnly DepartureDate { get; init; }

        /// <summary>
        /// Optional: Preferred departure time window.
        /// </summary>
        public TimeOnly? PreferredDepartureTime { get; init; }
    }

    /// <summary>
    /// Passenger information for flight search (minimal data).
    /// </summary>
    public sealed record PassengerRequest
    {
        /// <summary>
        /// Passenger type (adult, child, infant).
        /// </summary>
        [Required]
        public required PassengerType Type { get; init; }

        /// <summary>
        /// Age of passenger (required for children and infants).
        /// Adults: 18+, Children: 2-17, Infants: 0-1.
        /// </summary>
        [Range(0, 120, ErrorMessage = "Age must be between 0 and 120")]
        public int? Age { get; init; }
    }

    /// <summary>
    /// Cabin class options (vendor-neutral).
    /// </summary>
    public enum CabinClass
    {
        /// <summary>Economy class</summary>
        Economy,

        /// <summary>Premium economy class</summary>
        PremiumEconomy,

        /// <summary>Business class</summary>
        Business,

        /// <summary>First class</summary>
        First
    }

    /// <summary>
    /// Passenger type (vendor-neutral).
    /// </summary>
    public enum PassengerType
    {
        /// <summary>Adult passenger (18+ years)</summary>
        Adult,

        /// <summary>Child passenger (2-17 years)</summary>
        Child,

        /// <summary>Infant without seat (0-1 years)</summary>
        InfantWithoutSeat
    }
}
