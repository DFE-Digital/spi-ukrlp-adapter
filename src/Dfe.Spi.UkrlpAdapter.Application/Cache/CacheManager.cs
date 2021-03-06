using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.Events;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Application.Cache
{
    public interface ICacheManager
    {
        Task DownloadProvidersToCacheAsync(CancellationToken cancellationToken);
        Task ProcessBatchOfProviders(long[] ukprns, DateTime pointInTime, CancellationToken cancellationToken);

        Task TidyCacheAsync(CancellationToken cancellationToken);
    }

    public class CacheManager : ICacheManager
    {
        private readonly IStateRepository _stateRepository;
        private readonly IUkrlpApiClient _ukrlpApiClient;
        private readonly IProviderRepository _providerRepository;
        private readonly IMapper _mapper;
        private readonly IEventPublisher _eventPublisher;
        private readonly IProviderProcessingQueue _providerProcessingQueue;
        private readonly CacheConfiguration _configuration;
        private readonly ILoggerWrapper _logger;

        public CacheManager(
            IStateRepository stateRepository,
            IUkrlpApiClient ukrlpApiClient,
            IProviderRepository providerRepository,
            IMapper mapper,
            IEventPublisher eventPublisher,
            IProviderProcessingQueue providerProcessingQueue,
            CacheConfiguration configuration,
            ILoggerWrapper logger)
        {
            _stateRepository = stateRepository;
            _ukrlpApiClient = ukrlpApiClient;
            _providerRepository = providerRepository;
            _mapper = mapper;
            _eventPublisher = eventPublisher;
            _providerProcessingQueue = providerProcessingQueue;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task DownloadProvidersToCacheAsync(CancellationToken cancellationToken)
        {
            _logger.Info("Acquiring providers file from UKRLP...");
            var pointInTime = DateTime.UtcNow.Date;

            // Last read
            var lastRead = await _stateRepository.GetLastProviderReadTimeAsync(cancellationToken);

            // Download
            var providers = await _ukrlpApiClient.GetProvidersUpdatedSinceAsync(lastRead, cancellationToken);
            _logger.Info($"Read {providers.Length} providers from UKRLP that have been updated since {lastRead}");

            // Timestamp
            var pointInTimeProviders = providers.Select(establishment => establishment.Clone<PointInTimeProvider>()).ToArray();
            foreach (var pointInTimeEstablishment in pointInTimeProviders)
            {
                pointInTimeEstablishment.PointInTime = pointInTime;
            }

            // Store
            await _providerRepository.StoreInStagingAsync(pointInTimeProviders, cancellationToken);
            _logger.Debug($"Stored {providers.Length} providers in staging");

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

                _logger.Debug($"Queuing {position} to {position + batch.Length} for processing");
                await _providerProcessingQueue.EnqueueBatchOfStagingAsync(batch, pointInTime, cancellationToken);

                position += batchSize;
            }

            // Update last read
            lastRead = DateTime.Now;
            await _stateRepository.SetLastProviderReadTimeAsync(lastRead, cancellationToken);
            _logger.Info($"Set last read time to {lastRead}");

            _logger.Info("Finished downloading providers to cache");
        }

        public async Task ProcessBatchOfProviders(long[] ukprns, DateTime pointInTime, CancellationToken cancellationToken)
        {
            foreach (var ukprn in ukprns)
            {
                var previous = await _providerRepository.GetProviderAsync(ukprn, pointInTime, cancellationToken);
                var staging = await _providerRepository.GetProviderFromStagingAsync(ukprn, pointInTime, cancellationToken);

                if (previous == null)
                {
                    _logger.Info($"{ukprn} has not been seen before {pointInTime}. Processing as created");

                    await ProcessProvider(staging, _eventPublisher.PublishLearningProviderCreatedAsync,
                        cancellationToken);
                }
                else if (!AreSame(previous, staging))
                {
                    _logger.Info($"{ukprn} on {pointInTime} has changed since {previous.PointInTime}. Processing as updated");

                    await ProcessProvider(staging, _eventPublisher.PublishLearningProviderUpdatedAsync,
                        cancellationToken);
                }
                else
                {
                    _logger.Info($"{ukprn} on {pointInTime} has not changed since {previous.PointInTime}. Skipping");
                }
            }
        }

        public async Task TidyCacheAsync(CancellationToken cancellationToken)
        {
            var lastCleared = await _stateRepository.GetLastStagingDateClearedAsync(cancellationToken);
            var retentionDate = DateTime.Today.AddDays(-_configuration.NumberOfDaysToRetainStagingData);
            
            _logger.Info($"Tidying cache staging data between {lastCleared} and {retentionDate}");

            try
            {
                while (lastCleared < retentionDate)
                {
                    lastCleared = lastCleared.AddDays(1);

                    var numberOfRowsDeleted = await _providerRepository.ClearStagingDataForDateAsync(lastCleared, cancellationToken);

                    await _stateRepository.SetLastStagingDateClearedAsync(lastCleared, cancellationToken);
                    _logger.Info($"Cleared {numberOfRowsDeleted} rows for {lastCleared}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Errored when clearing data for {lastCleared} - {ex.Message}", ex);
            }
            
            _logger.Info($"Finished tidying cache staging data upto {lastCleared}");
        }

        private bool AreSame(Provider current, Provider staging)
        {
            if (current.ProviderName != staging.ProviderName ||
                current.AccessibleProviderName != staging.AccessibleProviderName ||
                current.ProviderVerificationDate != staging.ProviderVerificationDate ||
                current.ExpiryDate != staging.ExpiryDate ||
                current.ProviderStatus != staging.ProviderStatus)
            {
                return false;
            }

            if (!AreSame(current.ProviderContacts, staging.ProviderContacts))
            {
                return false;
            }

            return true;
        }
        private bool AreSame(ProviderContact[] current, ProviderContact[] staging)
        {
            if (current == null && staging == null)
            {
                return true;
            }

            var currentLegalAddress = current?.SingleOrDefault(c => c.ContactType == "L");
            var stagingLegalAddress = staging?.SingleOrDefault(c => c.ContactType == "L");
            if (!AreSame(currentLegalAddress, stagingLegalAddress))
            {
                return false;
            }
            
            var currentPrimaryContact = current?.SingleOrDefault(c => c.ContactType == "P");
            var stagingPrimaryContact = staging?.SingleOrDefault(c => c.ContactType == "P");
            return AreSame(currentPrimaryContact, stagingPrimaryContact);
        }
        private bool AreSame(ProviderContact current, ProviderContact staging)
        {
            if (current?.ContactRole != staging?.ContactRole ||
                current?.ContactTelephone1 != staging?.ContactTelephone1 ||
                current?.ContactTelephone2 != staging?.ContactTelephone2 ||
                current?.ContactFax != staging?.ContactFax ||
                current?.ContactWebsiteAddress != staging?.ContactWebsiteAddress ||
                current?.ContactEmail != staging?.ContactEmail)
            {
                return false;
            }

            if (!AreSame(current?.ContactAddress, staging?.ContactAddress))
            {
                return false;
            }

            return AreSame(current?.ContactPersonalDetails, staging?.ContactPersonalDetails);
        }
        private bool AreSame(AddressStructure current, AddressStructure staging)
        {
            if (current?.Address1 != staging?.Address1 ||
                current?.Address2 != staging?.Address2 ||
                current?.Address3 != staging?.Address3 ||
                current?.Address4 != staging?.Address4 ||
                current?.Town != staging?.Town ||
                current?.County != staging?.County ||
                current?.PostCode != staging?.PostCode)
            {
                return false;
            }
            
            return true;
        }
        private bool AreSame(PersonNameStructure current, PersonNameStructure staging)
        {
            if (current?.PersonNameTitle != staging?.PersonNameTitle ||
                current?.PersonGivenName != staging?.PersonGivenName ||
                current?.PersonFamilyName != staging?.PersonFamilyName ||
                current?.PersonNameSuffix != staging?.PersonNameSuffix ||
                current?.PersonRequestedName != staging?.PersonRequestedName)
            {
                return false;
            }
            
            return true;
        }

        private async Task ProcessProvider(
            PointInTimeProvider staging,
            Func<LearningProvider, DateTime, CancellationToken, Task> publishEvent,
            CancellationToken cancellationToken)
        {
            var current = await _providerRepository.GetProviderAsync(staging.UnitedKingdomProviderReferenceNumber, cancellationToken);
            
            staging.IsCurrent = current == null || staging.PointInTime > current.PointInTime;
            if (current != null && staging.IsCurrent)
            {
                current.IsCurrent = false;
            }

            var toStore = current == null || current.IsCurrent
                ? new[] {staging}
                : new[] {current, staging};
            
            await _providerRepository.StoreAsync(toStore, cancellationToken);
            _logger.Debug($"Stored {staging.UnitedKingdomProviderReferenceNumber} in repository");

            var learningProvider = await _mapper.MapAsync<LearningProvider>(staging, cancellationToken);
            await publishEvent(learningProvider, staging.PointInTime, cancellationToken);
            _logger.Debug($"Sent event for {staging.UnitedKingdomProviderReferenceNumber}");
        }
    }
}