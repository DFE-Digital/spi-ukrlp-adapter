using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Domain.Cache
{
    public interface IProviderRepository
    {
        Task StoreAsync(PointInTimeProvider provider, CancellationToken cancellationToken);
        Task StoreInStagingAsync(PointInTimeProvider[] providers, CancellationToken cancellationToken);
        Task<Provider> GetProviderAsync(long ukprn, CancellationToken cancellationToken);
        Task<PointInTimeProvider> GetProviderFromStagingAsync(long ukprn, DateTime pointInTime, CancellationToken cancellationToken);
        Task<Provider[]> GetProvidersAsync(long[] ukprns, CancellationToken cancellationToken);
        Task<Provider[]> GetProvidersAsync(CancellationToken cancellationToken);
    }
}