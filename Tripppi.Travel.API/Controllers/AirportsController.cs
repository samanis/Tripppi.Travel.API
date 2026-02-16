using Microsoft.AspNetCore.Mvc;
using Tripppi.Travel.API.Repositories;
using Tripppi.Travel.API.Domain;
using Tripppi.Travel.API.Services;

namespace Tripppi.Travel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AirportsController : ControllerBase
    {
        private readonly ILocationService _locationService;
        private readonly ILogger<AirportsController> _logger;

        public AirportsController(
            ILocationService locationService,
            ILogger<AirportsController> logger)
        {
            _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Search airports by prefix (matches airport name, IATA, city, country).
        /// Returns results ranked by text relevance (70%) and popularity (30%).
        /// </summary>
        /// <param name="q">Search query (e.g., "tor", "YYZ", "London", "char")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Top 5 matching airports ranked by combined score</returns>
        /// <response code="200">Returns matching airports</response>
        /// <response code="400">Invalid query parameter</response>
        /// <response code="404">No airports found matching the query</response>
        /// <response code="500">Internal server error</response>
        [HttpGet("search")]
        [ProducesResponseType(typeof(IReadOnlyList<Airport>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IReadOnlyList<Airport>>> Search(
            [FromQuery] string q,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { error = "Query parameter 'q' is required" });
            }

            _logger.LogInformation("Searching airports with query: {Query}", q);

            try
            {
                var airports = await _locationService.SearchAsync(q, cancellationToken);

                if (airports == null || airports.Count == 0)
                {
                    _logger.LogInformation("No airports found for query: {Query}", q);
                    return NotFound(new { error = $"No airports found matching '{q}'" });
                }

                return Ok(airports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching airports with query: {Query}", q);
                throw; // Let GlobalExceptionHandlerMiddleware handle it
            }
        }
    }
}
