using System;
using System.Linq;
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

            var location = provider.ProviderContacts.FirstOrDefault(c => c.ContactType == "L");

            var learningProvider = new LearningProvider
            {
                Name = provider.ProviderName,
                LegalName = provider.ProviderName,
                Ukprn = provider.UnitedKingdomProviderReferenceNumber,
                Postcode = location?.ContactAddress.PostCode,
                Urn = ReadVerificationValueAsLong(provider, "DfE (Schools Unique Reference Number)"),
                DfeNumber = ReadVerificationValue(provider, "DfE (LEA Code and Establishment Number)"),
                CompaniesHouseNumber = ReadVerificationValue(provider, "Companies House"),
                CharitiesCommissionNumber = ReadVerificationValue(provider, "Charity Commission"),
            };

            learningProvider.Status =
                await _translator.TranslateEnumValue(EnumerationNames.ProviderStatus, provider.ProviderStatus, cancellationToken);
            
            return learningProvider as TDestination;
        }

        private long? ReadVerificationValueAsLong(Provider provider, string verificationAuthority)
        {
            var value = ReadVerificationValue(provider, verificationAuthority);
            return !string.IsNullOrEmpty(value) ? (long?)long.Parse(value) : null;
        }
        private string ReadVerificationValue(Provider provider, string verificationAuthority)
        {
            var verificationDetails = provider.Verifications.SingleOrDefault(vd =>
                vd.Authority.Equals(verificationAuthority, StringComparison.InvariantCultureIgnoreCase));
            return verificationDetails?.Id;
        }
    }
}