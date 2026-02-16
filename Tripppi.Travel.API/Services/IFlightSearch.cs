using Tripppi.Travel.API.Services.DTOs;

namespace Tripppi.Travel.API.Services
{
    /// <summary>
    /// Vendor-neutral abstraction for flight search operations.
    /// Provides a unified interface for searching flights across different providers (Duffel, Amadeus, etc.).
    /// This abstraction allows switching between vendors without changing business logic.
    /// </summary>
    public interface IFlightSearch
    {
        /// <summary>
        /// Search for available flights based on the provided criteria.
        /// Returns a list of flight offers ranked by relevance and price.
        /// </summary>
        /// <param name="request">Flight search criteria including origin, destination, dates, and passengers.</param>
        /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
        /// <returns>
        /// A read-only list of flight search results with offers, pricing, and availability.
        /// Returns empty list if no flights found or search fails.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
        /// <exception cref="FlightSearchException">Thrown when search operation fails (timeout, vendor error, etc.).</exception>
        Task<IReadOnlyList<FlightSearchResult>> SearchFlightsAsync(
            FlightSearchRequest request, 
            CancellationToken cancellationToken = default);
    }
}
