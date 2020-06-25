using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Queue;
using Newtonsoft.Json;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache
{
    public class QueueProviderProcessingQueue : IProviderProcessingQueue
    {
        private CloudQueue _queue;
        
        public QueueProviderProcessingQueue(CacheConfiguration configuration)
        {
            var storageAccount = CloudStorageAccount.Parse(configuration.ProviderProcessingQueueConnectionString);
            var queueClient = storageAccount.CreateCloudQueueClient();
            _queue = queueClient.GetQueueReference(CacheQueueNames.ProviderProcessingQueue);
        }
        public async Task EnqueueBatchOfStagingAsync(long[] ukprns, DateTime pointInTime, CancellationToken cancellationToken)
        {
            await _queue.CreateIfNotExistsAsync(cancellationToken);

            var queueItem = new StagingBatchQueueItem
            {
                Identifiers = ukprns,
                PointInTime = pointInTime,
            };
                
            var message = new CloudQueueMessage(JsonConvert.SerializeObject(queueItem));
            await _queue.AddMessageAsync(message, cancellationToken);
        }
    }
}
