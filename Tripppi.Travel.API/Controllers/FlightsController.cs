using Microsoft.AspNetCore.Mvc;
using Tripppi.Travel.API.Services;
using Tripppi.Travel.API.Services.DTOs;

namespace Tripppi.Travel.API.Controllers
{
    /// <summary>
    /// Flight search and booking operations.
    /// Delegates to vendor-neutral IFlightSearch abstraction (currently Duffel).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class FlightsController : ControllerBase
    {
        private readonly IFlightSearch _flightSearch;
        private readonly ILogger<FlightsController> _logger;

        public FlightsController(
            IFlightSearch flightSearch,
            ILogger<FlightsController> logger)
        {
            _flightSearch = flightSearch ?? throw new ArgumentNullException(nameof(flightSearch));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Search for available flights based on search criteria.
        /// </summary>
        /// <param name="request">Flight search parameters (origin, destination, dates, passengers)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of flight offers with pricing and itinerary details</returns>
        /// <response code="200">Returns list of flight offers</response>
        /// <response code="400">Invalid search parameters</response>
        /// <response code="429">Rate limit exceeded</response>
        /// <response code="500">Internal server error</response>
        [HttpPost("search")]
        [ProducesResponseType(typeof(IReadOnlyList<FlightSearchResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IReadOnlyList<FlightSearchResult>>> SearchFlights(
            [FromBody] FlightSearchRequest request,
            CancellationToken cancellationToken)
        {
            // Validate model state
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid flight search request: {Errors}",
                    string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                return BadRequest(ModelState);
            }

            // Log search parameters (no PII)
            _logger.LogInformation(
                "Flight search request: {Origin} ? {Destination}, {LegCount} leg(s), {PassengerCount} passenger(s), Cabin: {CabinClass}",
                request.Legs[0].OriginIataCode,
                request.Legs[0].DestinationIataCode,
                request.Legs.Count,
                request.Passengers.Count,
                request.CabinClass);

            try
            {
                // Delegate to flight search abstraction
                var results = await _flightSearch.SearchFlightsAsync(request, cancellationToken);

                _logger.LogInformation("Flight search completed: {ResultCount} offers found", results.Count);

                // Return results (no PII - only offer IDs and flight details)
                return Ok(results);
            }
            catch (FlightSearchException ex)
            {
                _logger.LogError(ex,
                    "Flight search failed: ErrorCode={ErrorCode}, StatusCode={StatusCode}, Retryable={IsRetryable}",
                    ex.ErrorCode,
                    ex.StatusCode,
                    ex.IsRetryable);

                // Map to appropriate HTTP status code
                return ex.StatusCode switch
                {
                    400 => BadRequest(new ProblemDetails
                    {
                        Status = 400,
                        Title = "Invalid search parameters",
                        Detail = "Please check your search criteria and try again.",
                        Type = "https://docs.tripppi.com/errors/invalid-search"
                    }),
                    429 => StatusCode(429, new ProblemDetails
                    {
                        Status = 429,
                        Title = "Rate limit exceeded",
                        Detail = "Too many requests. Please try again later.",
                        Type = "https://docs.tripppi.com/errors/rate-limit"
                    }),
                    503 => StatusCode(503, new ProblemDetails
                    {
                        Status = 503,
                        Title = "Service temporarily unavailable",
                        Detail = ex.IsRetryable
                            ? "The flight search service is temporarily unavailable. Please retry."
                            : "Flight search failed. Please try again later.",
                        Type = "https://docs.tripppi.com/errors/service-unavailable"
                    }),
                    _ => StatusCode(500, new ProblemDetails
                    {
                        Status = 500,
                        Title = "Flight search failed",
                        Detail = "An error occurred while searching for flights. Please try again.",
                        Type = "https://docs.tripppi.com/errors/internal-error"
                    })
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Flight search cancelled by client");
                return StatusCode(499, new ProblemDetails
                {
                    Status = 499,
                    Title = "Request cancelled",
                    Detail = "The search request was cancelled.",
                    Type = "https://docs.tripppi.com/errors/cancelled"
                });
            }
            catch (Exception ex)
            {
                // Unexpected error - log but don't expose internals
                _logger.LogError(ex, "Unexpected error during flight search");
                return StatusCode(500, new ProblemDetails
                {
                    Status = 500,
                    Title = "Internal server error",
                    Detail = "An unexpected error occurred. Please try again later.",
                    Type = "https://docs.tripppi.com/errors/internal-error"
                });
            }
        }

        /// <summary>
        /// Health check endpoint for flight search service.
        /// </summary>
        /// <returns>Service status</returns>
        [HttpGet("health")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                service = "FlightSearch",
                status = "healthy",
                timestamp = DateTime.UtcNow,
                provider = "Duffel"
            });
        }
    }
}
