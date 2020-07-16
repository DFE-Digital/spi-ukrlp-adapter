using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Extensions;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Newtonsoft.Json;

namespace Dfe.Spi.UkrlpAdapter.Application.LearningProviders
{
    public interface ILearningProviderManager
    {
        Task<LearningProvider> GetLearningProviderAsync(string id, string fields, bool readFromLive, DateTime? pointInTime, CancellationToken cancellationToken);
        Task<LearningProvider[]> GetLearningProvidersAsync(string[] ids, string[] fields, bool readFromLive, DateTime? pointInTime, CancellationToken cancellationToken);
    }

    public class LearningProviderManager : ILearningProviderManager
    {
        private readonly IUkrlpApiClient _ukrlpApiClient;
        private readonly IProviderRepository _providerRepository;
        private readonly IMapper _mapper;
        private readonly ILoggerWrapper _logger;

        public LearningProviderManager(
            IUkrlpApiClient ukrlpApiClient, 
            IProviderRepository providerRepository,
            IMapper mapper, 
            ILoggerWrapper logger)
        {
            _ukrlpApiClient = ukrlpApiClient;
            _providerRepository = providerRepository;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<LearningProvider> GetLearningProviderAsync(string id, string fields, bool readFromLive, DateTime? pointInTime, CancellationToken cancellationToken)
        {
            long ukprn;
            if (!long.TryParse(id, out ukprn))
            {
                throw new ArgumentException($"id must be a number (ukprn) but received {id}", nameof(id));
            }

            if (id.Length != 8)
            {
                throw new ArgumentException($"UKPRN must be 8 digits but received {id.Length} ({id})");
            }

            var provider = readFromLive
                ? await _ukrlpApiClient.GetProviderAsync(ukprn, cancellationToken)
                : await _providerRepository.GetProviderAsync(ukprn, pointInTime, cancellationToken);
            if (provider == null)
            {
                return null;
            }

            _logger.Debug($"read provider {ukprn}: {JsonConvert.SerializeObject(provider)}");

            var requestedFields = string.IsNullOrEmpty(fields) ? null : fields.Split(',').Select(x => x.Trim()).ToArray();

            return await GetLearningProviderFromUkrlpProviderAsync(provider, requestedFields, cancellationToken);
        }

        public async Task<LearningProvider[]> GetLearningProvidersAsync(string[] ids, string[] fields, bool readFromLive, DateTime? pointInTime, CancellationToken cancellationToken)
        {
            var ukprns = new long[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                long ukprn;
                if (!long.TryParse(ids[i], out ukprn))
                {
                    throw new ArgumentException($"id must be a number (ukprn) but received {ids[i]} at index {i}", nameof(ids));
                }

                if (ids[i].Length != 8)
                {
                    throw new ArgumentException($"UKPRN must be 8 digits but received {ids[i].Length} ({ids[i]}) at index {i}", nameof(ids));
                }

                ukprns[i] = ukprn;
            }

            var providers = readFromLive
                ? await _ukrlpApiClient.GetProvidersAsync(ukprns, cancellationToken)
                : await _providerRepository.GetProvidersAsync(ukprns, pointInTime, cancellationToken);
            
            var learningProviders = new LearningProvider[ukprns.Length];
            for (var i = 0; i < ukprns.Length; i++)
            {
                var provider = providers.SingleOrDefault(p => p.UnitedKingdomProviderReferenceNumber == ukprns[i]);
                if (provider == null)
                {
                    continue;
                }
            
                learningProviders[i] = await GetLearningProviderFromUkrlpProviderAsync(provider, fields, cancellationToken);
            }

            return learningProviders;
        }


        private async Task<LearningProvider> GetLearningProviderFromUkrlpProviderAsync(Provider provider, string[] requestedFields,
            CancellationToken cancellationToken)
        {
            var learningProvider = await _mapper.MapAsync<LearningProvider>(provider, cancellationToken);
            _logger.Debug($"mapped provider {provider.UnitedKingdomProviderReferenceNumber} to {JsonConvert.SerializeObject(learningProvider)}");

            // If the fields are specified, then limit them... otherwise,
            // just return everything.
            if (requestedFields != null && requestedFields.Length > 0)
            {
                // Then we need to limit the fields we send back...
                string[] requestedFieldsUpper = requestedFields
                    .Select(x => x.ToUpperInvariant())
                    .ToArray();

                learningProvider =
                    learningProvider.PruneModel(requestedFields);

                // If lineage was requested then...
                if (learningProvider._Lineage != null)
                {
                    // ... prune the lineage too.
                    learningProvider._Lineage = learningProvider
                        ._Lineage
                        .Where(x => requestedFieldsUpper.Contains(x.Key.ToUpperInvariant()))
                        .ToDictionary(x => x.Key, x => x.Value);
                }

                _logger.Debug(
                    $"Pruned mapped establishment: {learningProvider}.");
            }
            else
            {
                _logger.Debug("No fields specified - model not pruned.");
            }

            return learningProvider;
        }
    }
}