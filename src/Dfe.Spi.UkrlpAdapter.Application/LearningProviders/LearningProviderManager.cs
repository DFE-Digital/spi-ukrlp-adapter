using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Newtonsoft.Json;

namespace Dfe.Spi.UkrlpAdapter.Application.LearningProviders
{
    public interface ILearningProviderManager
    {
        Task<LearningProvider> GetLearningProviderAsync(string id, CancellationToken cancellationToken);
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

        public async Task<LearningProvider> GetLearningProviderAsync(string id, CancellationToken cancellationToken)
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
            _logger.Info($"read provider {ukprn}: {JsonConvert.SerializeObject(provider)}");

            var learningProvider = await _mapper.MapAsync<LearningProvider>(provider, cancellationToken);
            _logger.Info($"mapped provider {ukprn} to {JsonConvert.SerializeObject(learningProvider)}");
            
            return learningProvider;
        }
    }
}