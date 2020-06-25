using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Http.Server.Definitions;
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
        private readonly IHttpSpiExecutionContextManager _httpSpiExecutionContextManager;
        private readonly ILoggerWrapper _logger;

        public ProcessBatchOfProviders(ICacheManager cacheManager, IHttpSpiExecutionContextManager httpSpiExecutionContextManager, ILoggerWrapper logger)
        {
            _cacheManager = cacheManager;
            _httpSpiExecutionContextManager = httpSpiExecutionContextManager;
            _logger = logger;
        }
        
        [StorageAccount("SPI_Cache:ProviderProcessingQueueConnectionString")]
        [FunctionName(FunctionName)]
        public async Task Run(
            [QueueTrigger(CacheQueueNames.ProviderProcessingQueue)]
            string queueContent, 
            CancellationToken cancellationToken)
        {
            _httpSpiExecutionContextManager.SetInternalRequestId(Guid.NewGuid());

            _logger.Info($"{FunctionName} trigger with: {queueContent}");

            var queueItem = JsonConvert.DeserializeObject<StagingBatchQueueItem>(queueContent);
            _logger.Debug($"Deserialized to {queueItem.Identifiers.Length} ukprns on {queueItem.PointInTime}");

            await _cacheManager.ProcessBatchOfProviders(queueItem.Identifiers, queueItem.PointInTime, cancellationToken);
        }
    }
}