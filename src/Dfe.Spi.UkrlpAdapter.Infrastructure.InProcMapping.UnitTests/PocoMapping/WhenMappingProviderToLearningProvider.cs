using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
using Dfe.Spi.Common.WellKnownIdentifiers;
using Dfe.Spi.Models;
using Dfe.Spi.UkrlpAdapter.Domain.Translation;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping;
using Moq;
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.UnitTests.PocoMapping
{
    public class WhenMappingProviderToLearningProvider
    {
        private Mock<ITranslator> _translatorMock;
        private ProviderMapper _mapper;
        private CancellationToken _cancellationToken;

        [SetUp]
        public void Arrange()
        {
            _translatorMock = new Mock<ITranslator>();
            _translatorMock.Setup(t =>
                    t.TranslateEnumValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("something");
            
            _mapper = new ProviderMapper(_translatorMock.Object);
            
            _cancellationToken = new CancellationToken();
        }
        
        [Test, AutoData]
        public async Task ThenItShouldReturnLearningProvider(Provider source)
        {
            var actual = await _mapper.MapAsync<LearningProvider>(source, _cancellationToken);

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<LearningProvider>(actual);
        }

        [Test, AutoData]
        public async Task ThenItShouldMapProviderToLearningProvider(Provider source, 
            AddressStructure address, string websiteAddress,
            long urn, string dfeNumber, string charityNumber, string companyNumber)
        {
            source.Verifications = new[]
            {
                new VerificationDetails {Authority = "DfE (Schools Unique Reference Number)", Id = urn.ToString()},
                new VerificationDetails {Authority = "DfE (LEA Code and Establishment Number)", Id = dfeNumber},
                new VerificationDetails {Authority = "Charity Commission", Id = charityNumber},
                new VerificationDetails {Authority = "Companies House", Id = companyNumber},
            };
            source.ProviderContacts = new[]
            {
                new ProviderContact
                {
                    ContactType = "L",
                    ContactAddress = address,
                }, 
                new ProviderContact
                {
                    ContactType = "P",
                    ContactWebsiteAddress = websiteAddress,
                }, 
            };
            
            var actual = await _mapper.MapAsync<LearningProvider>(source, _cancellationToken) as LearningProvider;

            Assert.IsNotNull(actual);
            Assert.AreEqual(source.ProviderName, actual.Name);
            Assert.AreEqual(source.UnitedKingdomProviderReferenceNumber, actual.Ukprn);
            Assert.AreEqual(address.PostCode, actual.Postcode);
            Assert.AreEqual(source.ProviderName, actual.LegalName);
            Assert.AreEqual(urn, actual.Urn);
            Assert.AreEqual(dfeNumber, actual.DfeNumber);
            Assert.AreEqual(companyNumber, actual.CompaniesHouseNumber);
            Assert.AreEqual(charityNumber, actual.CharitiesCommissionNumber);
            Assert.AreEqual(websiteAddress, actual.Website);
        }

        [Test, AutoData]
        public async Task ThenItShouldMapStatusFromTranslatedProviderValue(Provider source, string translatedValue)
        {
            _translatorMock.Setup(t =>
                    t.TranslateEnumValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(translatedValue);
            
            var actual = await _mapper.MapAsync<LearningProvider>(source, _cancellationToken) as LearningProvider;

            Assert.IsNotNull(actual);
            Assert.AreEqual(translatedValue, actual.Status);
            _translatorMock.Verify(t=>t.TranslateEnumValue(EnumerationNames.ProviderStatus, source.ProviderStatus, _cancellationToken),
                Times.Once);
        }

        [Test]
        public void ThenItShouldThrowExceptionIfSourceIsNotProvider()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _mapper.MapAsync<LearningProvider>(new object(), _cancellationToken));
        }

        [Test]
        public void ThenItShouldThrowExceptionIfDestinationIsNotLearningProvider()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _mapper.MapAsync<object>(new Provider(), _cancellationToken));
        }
    }
}