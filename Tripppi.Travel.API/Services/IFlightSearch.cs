using Tripppi.Travel.API.Services.DTOs;

namespace Tripppi.Travel.API.Services;

/// <summary>
/// Abstraction for flight search operations.
/// Implementations should delegate to specific providers (e.g., Duffel).
/// </summary>
public interface IFlightSearch
{
    /// <summary>
    /// Search for flights based on the provided criteria.
    /// </summary>
    /// <param name="request">Vendor-neutral search request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of flight offers</returns>
    Task<IReadOnlyList<DTOs.FlightSearchResult>> SearchFlightsAsync(
        DTOs.FlightSearchRequest request, 
        CancellationToken cancellationToken = default);
}
