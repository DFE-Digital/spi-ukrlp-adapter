using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AutoFixture.NUnit3;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
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
        private UkrlpSoapApiClient _client;

        [SetUp]
        public void Arrange()
        {
            _restClientMock = new Mock<IRestClient>();
            _restClientMock.Setup(c => c.ExecuteTaskAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetValidResponse(123, "Test"));

            _messageBuilderMock = new Mock<IUkrlpSoapMessageBuilder>();
            _messageBuilderMock.Setup(b => b.BuildMessageToGetSpecificUkprn(It.IsAny<long>()))
                .Returns("some-soap-xml-request");

            _configuration = new UkrlpApiConfiguration
            {
                Url = "https://ukrlp.test.local",
                StakeholderId = "123",
            };

            _client = new UkrlpSoapApiClient(_restClientMock.Object, _messageBuilderMock.Object, _configuration);
        }

        [Test, AutoData]
        public async Task ThenItShouldBuildMessageUsingRequestUkprn(long ukprn)
        {
            await _client.GetProviderAsync(ukprn, new CancellationToken());

            _messageBuilderMock.Verify(b => b.BuildMessageToGetSpecificUkprn(ukprn));
        }

        [Test, AutoData]
        public async Task ThenItShouldExecuteSoapRequestAgainstServer(long ukprn, string soapRequestMessage)
        {
            _messageBuilderMock.Setup(b => b.BuildMessageToGetSpecificUkprn(It.IsAny<long>()))
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
        public async Task ThenItShouldReturnDeserializedProvider(long ukprn, string providerName, string postcode,
            string status)
        {
            _restClientMock.Setup(c => c.ExecuteTaskAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GetValidResponse(ukprn, providerName, postcode, status));

            var actual = await _client.GetProviderAsync(ukprn, new CancellationToken());

            Assert.IsNotNull(actual);
            Assert.AreEqual(ukprn, actual.UnitedKingdomProviderReferenceNumber);
            Assert.AreEqual(providerName, actual.ProviderName);
            Assert.AreEqual(postcode, actual.Postcode);
            Assert.AreEqual(status, actual.ProviderStatus);
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

        private IRestResponse GetValidResponse(long ukprn, string establishmentName, string postcode = null,
            string status = null)
        {
            XNamespace ukrlpNs = "http://ukrlp.co.uk.server.ws.v3";

            var providerElement = new XElement("MatchingProviderRecords",
                new XElement("UnitedKingdomProviderReferenceNumber", ukprn),
                new XElement("ProviderName", establishmentName));
            if (!string.IsNullOrEmpty(postcode))
            {
                providerElement.Add(new XElement("ProviderContact",
                    new XElement("ContactType", "L"),
                    new XElement("ContactAddress",
                        new XElement("PostCode", postcode))));
            }

            if (!string.IsNullOrEmpty(status))
            {
                providerElement.Add(new XElement("ProviderStatus", status));
            }

            var envelope = GetSoapEnvelope(new XElement(ukrlpNs + "ProviderQueryResponse",
                new XAttribute(XNamespace.Xmlns + "ukrlp", ukrlpNs.NamespaceName),
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