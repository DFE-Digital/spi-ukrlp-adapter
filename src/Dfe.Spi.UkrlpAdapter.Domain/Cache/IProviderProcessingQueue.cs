using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dfe.Spi.UkrlpAdapter.Domain.Cache
{
    public interface IProviderProcessingQueue
    {
        Task EnqueueBatchOfStagingAsync(long[] ukprns, DateTime pointInTime, CancellationToken cancellationToken);
    }
}