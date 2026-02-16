using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tripppi.Travel.API.Domain;

namespace Tripppi.Travel.API.Services
{
    public interface ILocationService
    {
        Task<IReadOnlyList<Airport>> SearchAsync(string query, CancellationToken cancellationToken = default);
    }
}
