namespace Tripppi.Travel.API.Services.DTOs;

/// <summary>
/// Vendor-neutral flight search result.
/// </summary>
public sealed class FlightSearchResult
{
    /// <summary>
    /// Unique identifier for this offer (vendor-specific, e.g., Duffel offer_id).
    /// Use this ID for subsequent operations (reprice, book).
    /// </summary>
    public required string OfferId { get; init; }

    /// <summary>
    /// Total price for all passengers and services.
    /// </summary>
    public decimal TotalAmount { get; init; }

    /// <summary>
    /// Currency code (ISO 4217, e.g., "USD", "CAD").
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// Base fare (before taxes and fees).
    /// </summary>
    public decimal BaseAmount { get; init; }

    /// <summary>
    /// Total taxes and fees.
    /// </summary>
    public decimal TaxAmount { get; init; }

    /// <summary>
    /// When this offer expires and can no longer be booked.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Whether instant payment is required at booking.
    /// </summary>
    public bool RequiresInstantPayment { get; init; }

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
}
