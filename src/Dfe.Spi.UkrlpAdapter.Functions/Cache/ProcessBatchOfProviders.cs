using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Application.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;

namespace Dfe.Spi.UkrlpAdapter.Functions.Cache
{
    public class ProcessBatchOfProviders
    {
        private const string FunctionName = nameof(ProcessBatchOfProviders);

        private readonly ICacheManager _cacheManager;
        private readonly ILoggerWrapper _logger;

        public ProcessBatchOfProviders(ICacheManager cacheManager, ILoggerWrapper logger)
        {
            _cacheManager = cacheManager;
            _logger = logger;
        }
        
        [StorageAccount("SPI_Cache:ProviderProcessingQueueConnectionString")]
        [FunctionName(FunctionName)]
        public async Task Run(
            [QueueTrigger(CacheQueueNames.ProviderProcessingQueue)]
            string queueContent, 
            CancellationToken cancellationToken)
        {
            _logger.SetInternalRequestId(Guid.NewGuid());
            _logger.Info($"{FunctionName} trigger with: {queueContent}");

            var ukprns = JsonConvert.DeserializeObject<long[]>(queueContent);
            _logger.Debug($"Deserialized to {ukprns.Length} urns");

            await _cacheManager.ProcessBatchOfProviders(ukprns, cancellationToken);
        }
    }
}