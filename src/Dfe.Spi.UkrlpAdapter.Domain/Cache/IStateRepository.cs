using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dfe.Spi.UkrlpAdapter.Domain.Cache
{
    public interface IStateRepository
    {
        Task<DateTime> GetLastProviderReadTimeAsync(CancellationToken cancellationToken);
        Task SetLastProviderReadTimeAsync(DateTime lastRead, CancellationToken cancellationToken);
    }
}