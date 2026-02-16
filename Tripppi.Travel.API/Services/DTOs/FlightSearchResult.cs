namespace Tripppi.Travel.API.Services.DTOs
{
    /// <summary>
    /// Vendor-neutral flight search result.
    /// Represents a single flight offer with pricing, itinerary, and availability.
    /// </summary>
    public sealed record FlightSearchResult
    {
        /// <summary>
        /// Unique identifier for this offer (vendor-specific, e.g., Duffel offer_id).
        /// Use this ID for subsequent operations (reprice, book).
        /// </summary>
        public required string OfferId { get; init; }

        /// <summary>
        /// Total price for all passengers and services.
        /// </summary>
        public required decimal TotalAmount { get; init; }

        /// <summary>
        /// Currency code (ISO 4217, e.g., "USD", "CAD").
        /// </summary>
        public required string Currency { get; init; }

        /// <summary>
        /// Base fare (before taxes and fees).
        /// </summary>
        public required decimal BaseAmount { get; init; }

        /// <summary>
        /// Total taxes and fees.
        /// </summary>
        public required decimal TaxAmount { get; init; }

        /// <summary>
        /// When this offer expires and can no longer be booked.
        /// </summary>
        public required DateTime ExpiresAt { get; init; }

        /// <summary>
        /// Flight legs (outbound, return, etc.).
        /// Each leg represents a journey from origin to destination.
        /// </summary>
        public required List<FlightLegResult> Legs { get; init; }

        /// <summary>
        /// Per-passenger details (baggage, fare basis).
        /// </summary>
        public required List<PassengerResult> Passengers { get; init; }

        /// <summary>
        /// Fare conditions (change fees, refund policy).
        /// </summary>
        public required FareConditions Conditions { get; init; }

        /// <summary>
        /// Marketing airline operating this flight.
        /// </summary>
        public required AirlineInfo Owner { get; init; }

        /// <summary>
        /// Whether instant payment is required at booking.
        /// </summary>
        public required bool RequiresInstantPayment { get; init; }
    }

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

    /// <summary>
    /// Per-passenger details from search results.
    /// </summary>
    public sealed record PassengerResult
    {
        /// <summary>
        /// Passenger type (adult, child, infant).
        /// </summary>
        public required PassengerType Type { get; init; }

        /// <summary>
        /// Cabin class for this passenger.
        /// </summary>
        public required CabinClass CabinClass { get; init; }

        /// <summary>
        /// Marketing name of cabin (e.g., "Main Cabin", "Economy Plus").
        /// </summary>
        public string? CabinClassMarketingName { get; init; }

        /// <summary>
        /// Fare basis code (airline internal code).
        /// </summary>
        public string? FareBasisCode { get; init; }

        /// <summary>
        /// Baggage allowances for this passenger.
        /// </summary>
        public required List<BaggageAllowance> Baggages { get; init; }
    }

    /// <summary>
    /// Baggage allowance details.
    /// </summary>
    public sealed record BaggageAllowance
    {
        /// <summary>
        /// Baggage type (carry-on, checked).
        /// </summary>
        public required BaggageType Type { get; init; }

        /// <summary>
        /// Number of bags allowed.
        /// </summary>
        public required int Quantity { get; init; }
    }

    /// <summary>
    /// Baggage type.
    /// </summary>
    public enum BaggageType
    {
        /// <summary>Cabin/carry-on baggage</summary>
        CarryOn,

        /// <summary>Checked/hold baggage</summary>
        Checked
    }

    /// <summary>
    /// Fare rules and conditions.
    /// </summary>
    public sealed record FareConditions
    {
        /// <summary>
        /// Can flight be changed before departure?
        /// </summary>
        public required bool ChangeBeforeDepartureAllowed { get; init; }

        /// <summary>
        /// Change fee amount (if applicable).
        /// </summary>
        public decimal? ChangeBeforeDeparturePenaltyAmount { get; init; }

        /// <summary>
        /// Currency for change fee.
        /// </summary>
        public string? ChangeBeforeDeparturePenaltyCurrency { get; init; }

        /// <summary>
        /// Can flight be refunded before departure?
        /// </summary>
        public required bool RefundBeforeDepartureAllowed { get; init; }

        /// <summary>
        /// Can flight be changed after departure?
        /// </summary>
        public required bool ChangeAfterDepartureAllowed { get; init; }

        /// <summary>
        /// Can flight be refunded after departure?
        /// </summary>
        public required bool RefundAfterDepartureAllowed { get; init; }
    }

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
