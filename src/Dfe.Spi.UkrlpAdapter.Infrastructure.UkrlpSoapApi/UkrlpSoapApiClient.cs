using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi
{
    public class UkrlpSoapApiClient : IUkrlpApiClient
    {
        public async Task<Provider> GetProviderAsync(long ukprn, CancellationToken cancellationToken)
        {
            return new Provider
            {
                UnitedKingdomProviderReferenceNumber = ukprn,
                ProviderName = $"Provider {ukprn}",
            };
        }
    }
}