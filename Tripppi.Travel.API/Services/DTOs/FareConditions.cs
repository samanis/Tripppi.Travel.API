namespace Tripppi.Travel.API.Services.DTOs
{
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
}
