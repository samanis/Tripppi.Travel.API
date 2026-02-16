using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tripppi.Travel.API.Domain;
using Tripppi.Travel.API.Repositories;

namespace Tripppi.Travel.API.Services
{
    public sealed class LocationService : ILocationService
    {
        private readonly IAirportRepository _airportRepository;
        private readonly ILogger<LocationService> _logger;

        public LocationService(IAirportRepository airportRepository, ILogger<LocationService> logger)
        {
            _airportRepository = airportRepository ?? throw new ArgumentNullException(nameof(airportRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<Airport>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<Airport>();

            _logger.LogDebug("LocationService.SearchAsync query={Query}", query);

            // Delegate to repository scoring search which returns top results
            return await _airportRepository.SearchWithScoringAsync(query, cancellationToken).ConfigureAwait(false);
        }
    }
}
