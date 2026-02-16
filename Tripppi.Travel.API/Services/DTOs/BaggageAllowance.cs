namespace Tripppi.Travel.API.Services.DTOs
{
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
}
