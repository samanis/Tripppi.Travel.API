namespace Tripppi.Travel.API.Services.DTOs
{
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
}
