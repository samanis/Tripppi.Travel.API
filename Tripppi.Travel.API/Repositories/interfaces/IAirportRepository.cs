using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tripppi.Travel.API.Domain;

namespace Tripppi.Travel.API.Repositories
{
    /// <summary>
    /// Read-only repository contract for airport search queries.
    /// Uses cache-first strategy with database fallback.
    /// </summary>
    public interface IAirportRepository
    {
        /// <summary>
        /// Searches airports with relevance scoring across all fields.
        /// Combines text match (60%) and popularity score (40%) for ranking.
        /// Returns top 5 results ordered by combined score.
        /// </summary>
        /// <param name="query">Search query (minimum 2 characters)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Top 5 matching airports ranked by relevance and popularity</returns>
        Task<IReadOnlyList<Airport>> SearchWithScoringAsync(string query, CancellationToken cancellationToken = default);
    }
}