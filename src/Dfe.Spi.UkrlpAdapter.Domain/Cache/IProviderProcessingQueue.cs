using System.Threading;
using System.Threading.Tasks;

namespace Dfe.Spi.UkrlpAdapter.Domain.Cache
{
    public interface IProviderProcessingQueue
    {
        Task EnqueueBatchOfStagingAsync(long[] ukprns, CancellationToken cancellationToken);
    }
}