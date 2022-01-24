using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AutoFixture.NUnit3;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Moq;
using NUnit.Framework;
using RestSharp;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi.UnitTests
{
    public class WhenGettingProvider
    {
        private Mock<IRestClient> _restClientMock;
        private Mock<IUkrlpSoapMessageBuilder> _messageBuilderMock;
        private UkrlpApiConfiguration _configuration;
        private Mock<ILoggerWrapper> _loggerMock;
        private UkrlpSoapApiClient _client;

        [SetUp]
        public void Arrange()
        {
            _restClientMock = new Mock<IRestClient>();
            _restClientMock.Setup(c => c.ExecuteTaskAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetValidResponse(123, "Test"));

            _messageBuilderMock = new Mock<IUkrlpSoapMessageBuilder>();
            _messageBuilderMock.Setup(b => b.BuildMessageToGetSpecificUkprns(It.IsAny<long[]>(), It.IsAny<string>()))
                .Returns("some-soap-xml-request");

            _configuration = new UkrlpApiConfiguration
            {
                Url = "https://ukrlp.test.local",
                StakeholderId = "123",
            };

            _loggerMock = new Mock<ILoggerWrapper>();

            _client = new UkrlpSoapApiClient(_restClientMock.Object, _messageBuilderMock.Object, _configuration, _loggerMock.Object);
        }

        [Test, AutoData]
        public async Task ThenItShouldBuildMessageUsingRequestUkprn(long ukprn)
        {
            await _client.GetProviderAsync(ukprn, new CancellationToken());

            _messageBuilderMock.Verify(b => b.BuildMessageToGetSpecificUkprns(new[]{ukprn}, "A"));
        }

        [Test, AutoData]
        public async Task ThenItShouldExecuteSoapRequestAgainstServer(long ukprn, string soapRequestMessage)
        {
            _messageBuilderMock.Setup(b => b.BuildMessageToGetSpecificUkprns(It.IsAny<long[]>(), It.IsAny<string>()))
                .Returns(soapRequestMessage);

            await _client.GetProviderAsync(ukprn, new CancellationToken());

            var expectedSoapAction = "retrieveAllProviders";
            _restClientMock.Verify(c => c.ExecuteTaskAsync(It.Is<IRestRequest>(r =>
                    r.Method == Method.POST &&
                    r.Parameters.Any(p => p.Name == "SOAPAction") &&
                    (string) r.Parameters.Single(p => p.Name == "SOAPAction").Value == expectedSoapAction &&
                    r.Parameters.Any(p => p.Type == ParameterType.RequestBody) &&
                    r.Parameters.Single(p => p.Type == ParameterType.RequestBody).Value == soapRequestMessage),
                It.IsAny<CancellationToken>()));
        }

        [Test, AutoData]
        public async Task ThenItShouldReturnDeserializedProvider(Provider provider)
        {
            _restClientMock.Setup(c => c.ExecuteTaskAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetValidResponse(provider));

            var actual = await _client.GetProviderAsync(provider.UnitedKingdomProviderReferenceNumber, new CancellationToken());

            Assert.IsNotNull(actual);
            Assert.AreEqual(provider.UnitedKingdomProviderReferenceNumber, actual.UnitedKingdomProviderReferenceNumber);
            Assert.AreEqual(provider.ProviderName, actual.ProviderName);
            Assert.AreEqual(provider.AccessibleProviderName, actual.AccessibleProviderName);
            Assert.AreEqual(provider.ProviderVerificationDate, actual.ProviderVerificationDate);
            Assert.AreEqual(provider.ExpiryDate, actual.ExpiryDate);
            Assert.AreEqual(provider.ProviderStatus, actual.ProviderStatus);
            
            Assert.IsNotNull(actual.ProviderContacts);
            Assert.AreEqual(provider.ProviderContacts.Length, actual.ProviderContacts.Length);
            for (var i = 0; i < provider.ProviderContacts.Length; i++)
            {
                var expectedContact = provider.ProviderContacts[i];
                var actualContact = actual.ProviderContacts[i];
                var message = $"Contact {i} is not valid";
                
                Assert.AreEqual(expectedContact.ContactType, actualContact.ContactType, message);
                Assert.AreEqual(expectedContact.ContactRole, actualContact.ContactRole, message);
                Assert.AreEqual(expectedContact.ContactTelephone1, actualContact.ContactTelephone1, message);
                Assert.AreEqual(expectedContact.ContactTelephone2, actualContact.ContactTelephone2, message);
                Assert.AreEqual(expectedContact.ContactFax, actualContact.ContactFax, message);
                Assert.AreEqual(expectedContact.ContactWebsiteAddress, actualContact.ContactWebsiteAddress, message);
                Assert.AreEqual(expectedContact.ContactEmail, actualContact.ContactEmail, message);
                Assert.AreEqual(expectedContact.LastUpdated, actualContact.LastUpdated, message);
                
                Assert.IsNotNull(expectedContact.ContactAddress, message);
                Assert.AreEqual(expectedContact.ContactAddress.Address1, actualContact.ContactAddress.Address1, message);
                Assert.AreEqual(expectedContact.ContactAddress.Address2, actualContact.ContactAddress.Address2, message);
                Assert.AreEqual(expectedContact.ContactAddress.Address3, actualContact.ContactAddress.Address3, message);
                Assert.AreEqual(expectedContact.ContactAddress.Address4, actualContact.ContactAddress.Address4, message);
                Assert.AreEqual(expectedContact.ContactAddress.Town, actualContact.ContactAddress.Town, message);
                Assert.AreEqual(expectedContact.ContactAddress.County, actualContact.ContactAddress.County, message);
                Assert.AreEqual(expectedContact.ContactAddress.PostCode, actualContact.ContactAddress.PostCode, message);
                
                Assert.IsNotNull(expectedContact.ContactPersonalDetails, message);
                Assert.AreEqual(expectedContact.ContactPersonalDetails.PersonNameTitle, actualContact.ContactPersonalDetails.PersonNameTitle, message);
                Assert.AreEqual(expectedContact.ContactPersonalDetails.PersonGivenName, actualContact.ContactPersonalDetails.PersonGivenName, message);
                Assert.AreEqual(expectedContact.ContactPersonalDetails.PersonFamilyName, actualContact.ContactPersonalDetails.PersonFamilyName, message);
                Assert.AreEqual(expectedContact.ContactPersonalDetails.PersonNameSuffix, actualContact.ContactPersonalDetails.PersonNameSuffix, message);
                Assert.AreEqual(expectedContact.ContactPersonalDetails.PersonRequestedName, actualContact.ContactPersonalDetails.PersonRequestedName, message);
            }
            
            Assert.IsNotNull(actual.Verifications);
            Assert.AreEqual(provider.Verifications.Length, actual.Verifications.Length);
        }

        [Test, AutoData]
        public async Task ThenItShouldReturnNullIsNoProviderFound(long ukprn)
        {
            _restClientMock.Setup(c => c.ExecuteTaskAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetEmptyResponse());

            var actual = await _client.GetProviderAsync(ukprn, new CancellationToken());

            Assert.IsNull(actual);
        }

        [Test, AutoData]
        public void ThenItShouldThrowExceptionIfSoapFaultReceived(long urn, string faultCode, string faultString)
        {
            _restClientMock.Setup(c => c.ExecuteTaskAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetFaultResponse(faultCode, faultString));

            var actual = Assert.ThrowsAsync<SoapException>(async () =>
                await _client.GetProviderAsync(urn, new CancellationToken()));
            Assert.AreEqual(faultCode, actual.FaultCode);
            Assert.AreEqual(faultString, actual.Message);
        }


        private XNamespace soapNs = "http://schemas.xmlsoap.org/soap/envelope/";

        private IRestResponse GetValidResponse(long ukprn, string providerName)
        {
            return GetValidResponse(new Provider
            {
                UnitedKingdomProviderReferenceNumber = ukprn,
                ProviderName = providerName,
            });
        }
        private IRestResponse GetValidResponse(Provider provider)
        {
            XNamespace ns2 = "http://www.govtalk.gov.uk/people/bs7666";
            XNamespace ns3 = "http://www.govtalk.gov.uk/people/PersonDescriptives";
            XNamespace ns4 = "http://ukrlp.co.uk.server.ws.v3";

            var providerElement = new XElement("MatchingProviderRecords",
                new XElement("UnitedKingdomProviderReferenceNumber", provider.UnitedKingdomProviderReferenceNumber),
                new XElement("ProviderName", provider.ProviderName));
            if (!string.IsNullOrEmpty(provider.AccessibleProviderName))
            {
                providerElement.Add(new XElement("AccessibleProviderName", provider.AccessibleProviderName));
            }
            if (provider.ProviderVerificationDate.HasValue)
            {
                providerElement.Add(new XElement("ProviderVerificationDate", provider.ProviderVerificationDate.Value.ToString("O")));
            }
            if (provider.ExpiryDate.HasValue)
            {
                providerElement.Add(new XElement("ExpiryDate", provider.ExpiryDate.Value.ToString("O")));
            }

            var providerContacts = provider.ProviderContacts ?? new ProviderContact[0];
            foreach (var providerContact in providerContacts)
            {
                var contactElement = new XElement("ProviderContact",
                    new XElement("ContactType", providerContact.ContactType),
                    new XElement("ContactRole", providerContact.ContactRole),
                    new XElement("ContactTelephone1", providerContact.ContactTelephone1),
                    new XElement("ContactTelephone2", providerContact.ContactTelephone2),
                    new XElement("ContactFax", providerContact.ContactFax),
                    new XElement("ContactWebsiteAddress", providerContact.ContactWebsiteAddress),
                    new XElement("ContactEmail", providerContact.ContactEmail),
                    new XElement("LastUpdated", providerContact.LastUpdated));

                if (providerContact.ContactAddress != null)
                {
                    var addressElement = new XElement("ContactAddress",
                        new XElement("Address1", providerContact.ContactAddress.Address1),
                        new XElement("Address2", providerContact.ContactAddress.Address2),
                        new XElement("Address3", providerContact.ContactAddress.Address3),
                        new XElement("Address4", providerContact.ContactAddress.Address4),
                        new XElement("Town", providerContact.ContactAddress.Town),
                        new XElement("County", providerContact.ContactAddress.County),
                        new XElement("PostCode", providerContact.ContactAddress.PostCode));
                    contactElement.Add(addressElement);
                }

                if (providerContact.ContactPersonalDetails != null)
                {
                    var personalDetailsElement = new XElement("ContactPersonalDetails",
                        new XElement(ns3 + "PersonNameTitle", providerContact.ContactPersonalDetails.PersonNameTitle),
                        new XElement(ns3 + "PersonGivenName", providerContact.ContactPersonalDetails.PersonGivenName),
                        new XElement(ns3 + "PersonFamilyName", providerContact.ContactPersonalDetails.PersonFamilyName),
                        new XElement(ns3 + "PersonNameSuffix", providerContact.ContactPersonalDetails.PersonNameSuffix),
                        new XElement(ns3 + "PersonRequestedName", providerContact.ContactPersonalDetails.PersonRequestedName));
                    contactElement.Add(personalDetailsElement);
                }
                
                providerElement.Add(contactElement);
            }

            var verifications = provider.Verifications ?? new VerificationDetails[0];
            foreach (var verification in verifications)
            {
                providerElement.Add(new XElement("VerificationDetails",
                    new XElement("VerificationAuthority", verification.Authority),
                    new XElement("VerificationID", verification.Id)));
            }

            if (!string.IsNullOrEmpty(provider.ProviderStatus))
            {
                providerElement.Add(new XElement("ProviderStatus", provider.ProviderStatus));
            }

            var envelope = GetSoapEnvelope(new XElement(ns4 + "ProviderQueryResponse",
                new XAttribute(XNamespace.Xmlns + "ukrlp", ns4.NamespaceName),
                providerElement));

            var responseMock = new Mock<IRestResponse>();
            responseMock.Setup(r => r.Content).Returns(envelope.ToString());
            responseMock.Setup(r => r.IsSuccessful).Returns(true);
            return responseMock.Object;
        }

        private IRestResponse GetEmptyResponse()
        {
            XNamespace ukrlpNs = "http://ukrlp.co.uk.server.ws.v3";

            var envelope = GetSoapEnvelope(new XElement(ukrlpNs + "ProviderQueryResponse",
                new XAttribute(XNamespace.Xmlns + "ukrlp", ukrlpNs.NamespaceName)));

            var responseMock = new Mock<IRestResponse>();
            responseMock.Setup(r => r.Content).Returns(envelope.ToString());
            responseMock.Setup(r => r.IsSuccessful).Returns(true);
            return responseMock.Object;
        }

        private IRestResponse GetFaultResponse(string faultCode, string faultString)
        {
            var envelope = GetSoapEnvelope(new XElement(soapNs + "Fault",
                new XElement("faultcode", faultCode),
                new XElement("faultstring", faultString)));

            var responseMock = new Mock<IRestResponse>();
            responseMock.Setup(r => r.Content).Returns(envelope.ToString());
            responseMock.Setup(r => r.IsSuccessful).Returns(false);
            return responseMock.Object;
        }

        private XElement GetSoapEnvelope(XElement bodyContent)
        {
            return new XElement(soapNs + "Envelope",
                new XAttribute(XNamespace.Xmlns + "soapenv", soapNs.NamespaceName),
                new XElement(soapNs + "Body",
                    bodyContent));
        }
    }
}