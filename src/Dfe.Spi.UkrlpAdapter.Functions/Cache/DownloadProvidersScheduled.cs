using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Http.Server.Definitions;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Application.Cache;
using Microsoft.Azure.WebJobs;

namespace Dfe.Spi.UkrlpAdapter.Functions.Cache
{
    public class DownloadProvidersScheduled
    {
        private const string FunctionName = nameof(DownloadProvidersScheduled);
        private const string ScheduleExpression = "%SPI_Cache:ProviderSchedule%";

        private readonly ICacheManager _cacheManager;
        private readonly IHttpSpiExecutionContextManager _httpSpiExecutionContextManager;
        private readonly ILoggerWrapper _logger;

        public DownloadProvidersScheduled(ICacheManager cacheManager, IHttpSpiExecutionContextManager httpSpiExecutionContextManager, ILoggerWrapper logger)
        {
            _cacheManager = cacheManager;
            _httpSpiExecutionContextManager = httpSpiExecutionContextManager;
            _logger = logger;
        }
        
        [FunctionName(FunctionName)]
        public async Task Run([TimerTrigger(ScheduleExpression)] TimerInfo timerInfo, CancellationToken cancellationToken)
        {
            _httpSpiExecutionContextManager.SetInternalRequestId(Guid.NewGuid());
            
            _logger.Info($"{FunctionName} started at {DateTime.UtcNow}. Past due: {timerInfo.IsPastDue}");

            await _cacheManager.DownloadProvidersToCacheAsync(cancellationToken);
        }
    }
}