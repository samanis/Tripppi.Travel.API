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
        /// Returns results ranked by text relevance (60%) and popularity (40%).
        /// </summary>
        /// <param name="q">Search query (e.g., "tor", "YYZ", "London", "char")</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Top 5 matching airports ranked by combined score</returns>
        [HttpGet("search")]
        public async Task<ActionResult<IReadOnlyList<Airport>>> Search(
            [FromQuery] string q,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest(new { error = "Query parameter 'q' is required" });
            }

            _logger.LogInformation("Searching airports with query: {Query}", q);

            var airports = await _locationService.SearchAsync(q, cancellationToken);

            return Ok(airports);
        }
    }
}
