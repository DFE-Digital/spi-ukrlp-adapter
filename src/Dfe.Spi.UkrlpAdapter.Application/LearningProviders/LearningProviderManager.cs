using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Extensions;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Newtonsoft.Json;

namespace Dfe.Spi.UkrlpAdapter.Application.LearningProviders
{
    public interface ILearningProviderManager
    {
        Task<LearningProvider> GetLearningProviderAsync(string id, string fields, CancellationToken cancellationToken);
        Task<LearningProvider[]> GetLearningProvidersAsync(string[] ids, string[] fields, CancellationToken cancellationToken);
    }
    public class LearningProviderManager : ILearningProviderManager
    {
        private readonly IUkrlpApiClient _ukrlpApiClient;
        private readonly IMapper _mapper;
        private readonly ILoggerWrapper _logger;

        public LearningProviderManager(IUkrlpApiClient ukrlpApiClient, IMapper mapper, ILoggerWrapper logger)
        {
            _ukrlpApiClient = ukrlpApiClient;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<LearningProvider> GetLearningProviderAsync(string id, string fields, CancellationToken cancellationToken)
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

            var provider = await _ukrlpApiClient.GetProviderAsync(ukprn, cancellationToken);
            if (provider == null)
            {
                return null;
            }
            _logger.Debug($"read provider {ukprn}: {JsonConvert.SerializeObject(provider)}");

            var requestedFields = string.IsNullOrEmpty(fields) ? null : fields.Split(',').Select(x => x.Trim()).ToArray();

            return await GetLearningProviderFromUkrlpProviderAsync(provider, requestedFields, cancellationToken);
        }

        public async Task<LearningProvider[]> GetLearningProvidersAsync(string[] ids, string[] fields, CancellationToken cancellationToken)
        {
            long[] ukprns = new long[ids.Length];
            for (var i = 0; i < ids.Length; i++)
            {
                long ukprn;
                if (!long.TryParse(ids[i], out ukprn))
                {
                    throw new ArgumentException($"id must be a number (ukprn) but received {ids[i]} at index {i}", nameof(ids));
                }

                ukprns[i] = ukprn;
            }

            var providers = await _ukrlpApiClient.GetProvidersAsync(ukprns, cancellationToken);
            var learningProviders = new LearningProvider[providers.Length];

            for (var i = 0; i < providers.Length; i++)
            {
                if (providers[i] == null)
                {
                    continue;
                }

                learningProviders[i] = await GetLearningProviderFromUkrlpProviderAsync(providers[i], fields, cancellationToken);
            }

            return learningProviders;
        }


        private async Task<LearningProvider> GetLearningProviderFromUkrlpProviderAsync(Provider provider, string[] requestedFields, CancellationToken cancellationToken)
        {
            var learningProvider = await _mapper.MapAsync<LearningProvider>(provider, cancellationToken);
            _logger.Debug($"mapped provider {provider.UnitedKingdomProviderReferenceNumber} to {JsonConvert.SerializeObject(learningProvider)}");

            // If the fields are specified, then limit them... otherwise,
            // just return everything.
            if (requestedFields!=null && requestedFields.Length > 0)
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