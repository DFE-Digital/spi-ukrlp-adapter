using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi
{
    public interface IUkrlpApiClient
    {
        Task<Provider> GetProviderAsync(long ukprn, CancellationToken cancellationToken);
        Task<Provider[]> GetProvidersAsync(long[] ukprns, CancellationToken cancellationToken);
        Task<Provider[]> GetProvidersUpdatedSinceAsync(DateTime updatedSince, CancellationToken cancellationToken);
    }
}