using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Models;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Events;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Application.Cache
{
    public interface ICacheManager
    {
        Task DownloadProvidersToCacheAsync(CancellationToken cancellationToken);
        Task ProcessBatchOfProviders(long[] ukprns, CancellationToken cancellationToken);
    }

    public class CacheManager : ICacheManager
    {
        private readonly IStateRepository _stateRepository;
        private readonly IUkrlpApiClient _ukrlpApiClient;
        private readonly IProviderRepository _providerRepository;
        private readonly IMapper _mapper;
        private readonly IEventPublisher _eventPublisher;
        private readonly IProviderProcessingQueue _providerProcessingQueue;
        private readonly ILoggerWrapper _logger;

        public CacheManager(
            IStateRepository stateRepository,
            IUkrlpApiClient ukrlpApiClient,
            IProviderRepository providerRepository,
            IMapper mapper,
            IEventPublisher eventPublisher,
            IProviderProcessingQueue providerProcessingQueue,
            ILoggerWrapper logger)
        {
            _stateRepository = stateRepository;
            _ukrlpApiClient = ukrlpApiClient;
            _providerRepository = providerRepository;
            _mapper = mapper;
            _eventPublisher = eventPublisher;
            _providerProcessingQueue = providerProcessingQueue;
            _logger = logger;
        }

        public async Task DownloadProvidersToCacheAsync(CancellationToken cancellationToken)
        {
            _logger.Info("Acquiring providers file from UKRLP...");

            // Last read
            var lastRead = await _stateRepository.GetLastProviderReadTimeAsync(cancellationToken);

            // Download
            var providers = await _ukrlpApiClient.GetProvidersUpdatedSinceAsync(lastRead, cancellationToken);
            _logger.Info($"Read {providers.Length} providers from UKRLP that have been updated since {lastRead}");

            // Store
            await _providerRepository.StoreInStagingAsync(providers, cancellationToken);
            _logger.Info($"Stored {providers.Length} providers in staging");

            // Queue diff check
            var position = 0;
            const int batchSize = 100;
            while (position < providers.Length)
            {
                var batch = providers
                    .Skip(position)
                    .Take(batchSize)
                    .Select(e => e.UnitedKingdomProviderReferenceNumber)
                    .ToArray();

                _logger.Debug(
                    $"Queuing {position} to {position + batch.Length} for processing");
                await _providerProcessingQueue.EnqueueBatchOfStagingAsync(batch, cancellationToken);

                position += batchSize;
            }

            // Update last read
            lastRead = DateTime.Now;
            await _stateRepository.SetLastProviderReadTimeAsync(lastRead, cancellationToken);
            _logger.Info($"Set last read time to {lastRead}");

            _logger.Info("Finished downloading providers to cache");
        }

        public async Task ProcessBatchOfProviders(long[] ukprns, CancellationToken cancellationToken)
        {
            foreach (var ukprn in ukprns)
            {
                var current = await _providerRepository.GetProviderAsync(ukprn, cancellationToken);
                var staging = await _providerRepository.GetProviderFromStagingAsync(ukprn, cancellationToken);

                if (current == null)
                {
                    _logger.Info($"{ukprn} has not been seen before. Processing as created");

                    await ProcessProvider(staging, _eventPublisher.PublishLearningProviderCreatedAsync,
                        cancellationToken);
                }
                else if (!AreSame(current, staging))
                {
                    _logger.Info($"{ukprn} has changed. Processing as updated");

                    await ProcessProvider(staging, _eventPublisher.PublishLearningProviderUpdatedAsync,
                        cancellationToken);
                }
                else
                {
                    _logger.Info($"{ukprn} has not changed. Skipping");
                }
            }
        }

        private bool AreSame(Provider current, Provider staging)
        {
            if (current.ProviderName != staging.ProviderName)
            {
                return false;
            }

            if (current.Postcode != staging.Postcode)
            {
                return false;
            }

            return true;
        }

        private async Task ProcessProvider(Provider staging,
            Func<LearningProvider, CancellationToken, Task> publishEvent,
            CancellationToken cancellationToken)
        {
            await _providerRepository.StoreAsync(staging, cancellationToken);
            _logger.Debug($"Stored {staging.UnitedKingdomProviderReferenceNumber} in repository");

            var learningProvider = await _mapper.MapAsync<LearningProvider>(staging, cancellationToken);
            await publishEvent(learningProvider, cancellationToken);
            _logger.Debug($"Sent event for {staging.UnitedKingdomProviderReferenceNumber}");
        }
    }
}