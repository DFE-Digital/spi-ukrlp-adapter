using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.WellKnownIdentifiers;
using Dfe.Spi.Models;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Domain.Translation;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping
{
    internal class ProviderMapper : ObjectMapper
    {
        private static PropertyInfo[] _propertyInfos;

        private readonly ITranslator _translator;

        public ProviderMapper(ITranslator translator)
        {
            _propertyInfos = typeof(LearningProvider).GetProperties();

            _translator = translator;
        }

        internal override async Task<TDestination> MapAsync<TDestination>(object source,
            CancellationToken cancellationToken)
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
                    $"TDestination must be LearningProvider, but received {typeof(TDestination).FullName}",
                    nameof(source));
            }

            var legalAddress = provider.ProviderContacts.FirstOrDefault(c => c.ContactType == "L");
            var primaryContact = provider.ProviderContacts.FirstOrDefault(c => c.ContactType == "P");

            var telephones = new[]
            {
                legalAddress?.ContactTelephone1,
                legalAddress?.ContactTelephone2,
                primaryContact?.ContactTelephone1,
                primaryContact?.ContactTelephone2,
            };

            var learningProvider = new LearningProvider
            {
                Name = provider.ProviderName,
                LegalName = provider.ProviderName,
                Ukprn = provider.UnitedKingdomProviderReferenceNumber,
                Postcode = legalAddress?.ContactAddress.PostCode,
                Urn = ReadVerificationValueAsLong(provider, "DfE (Schools Unique Reference Number)"),
                DfeNumber = ReadVerificationValue(provider, "DfE (LEA Code and Establishment Number)"),
                CompaniesHouseNumber = ReadVerificationValue(provider, "Companies House"),
                CharitiesCommissionNumber = ReadVerificationValue(provider, "Charity Commission"),
                Website = primaryContact?.ContactWebsiteAddress,
                TelephoneNumber = telephones.FirstOrDefault(t => !string.IsNullOrEmpty(t)),
                ContactEmail = legalAddress?.ContactEmail ?? primaryContact?.ContactEmail,
                AddressLine1 = legalAddress?.ContactAddress?.Address1,
                AddressLine2 = legalAddress?.ContactAddress?.Address2,
                AddressLine3 = legalAddress?.ContactAddress?.Address3,
                Town = legalAddress?.ContactAddress?.Town,
                County = legalAddress?.ContactAddress?.County,
            };

            DateTime readDate = DateTime.UtcNow;

            // This is is about as complicated as it gets for now.
            // When we do stuff with management groups, might have to get a
            // little more involved.
            Dictionary<string, LineageEntry> lineage =
                _propertyInfos
                    .Where(x => !x.Name.StartsWith("_") && (x.GetValue(learningProvider) != null))
                    .ToDictionary(
                        x => x.Name,
                        x => new LineageEntry()
                        {
                            ReadDate = readDate,
                        });

            learningProvider._Lineage = lineage;

            learningProvider.Status =
                await _translator.TranslateEnumValue(EnumerationNames.ProviderStatus, provider.ProviderStatus,
                    cancellationToken);

            return learningProvider as TDestination;
        }

        private long? ReadVerificationValueAsLong(Provider provider, string verificationAuthority)
        {
            var value = ReadVerificationValue(provider, verificationAuthority);
            return !string.IsNullOrEmpty(value) ? (long?) long.Parse(value) : null;
        }

        private string ReadVerificationValue(Provider provider, string verificationAuthority)
        {
            var verificationDetails = provider.Verifications.SingleOrDefault(vd =>
                vd.Authority.Equals(verificationAuthority, StringComparison.InvariantCultureIgnoreCase));
            return verificationDetails?.Id;
        }
    }
}