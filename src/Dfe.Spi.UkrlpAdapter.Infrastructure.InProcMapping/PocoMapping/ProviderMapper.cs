using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.WellKnownIdentifiers;
using Dfe.Spi.Models;
using Dfe.Spi.UkrlpAdapter.Domain.Translation;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping
{
    internal class ProviderMapper : ObjectMapper
    {
        private readonly ITranslator _translator;

        public ProviderMapper(ITranslator translator)
        {
            _translator = translator;
        }

        internal override async Task<TDestination> MapAsync<TDestination>(object source, CancellationToken cancellationToken)
        {
            var provider = source as Provider;
            if (provider == null)
            {
                throw new ArgumentException(
                    $"source must be a Provider, but received {source.GetType().FullName}", nameof(source));
            }

            if (typeof(TDestination) != typeof(LearningProvider))
            {
                throw new ArgumentException(
                    $"TDestination must be LearningProvider, but received {typeof(TDestination).FullName}", nameof(source));
            }

            var learningProvider = new LearningProvider
            {
                Name = provider.ProviderName,
                LegalName = provider.ProviderName,
                Ukprn = provider.UnitedKingdomProviderReferenceNumber,
                Postcode = provider.Postcode,
            };

            learningProvider.Status =
                await _translator.TranslateEnumValue(EnumerationNames.ProviderStatus, provider.ProviderStatus, cancellationToken);
            
            return learningProvider as TDestination;
        }
    }
}