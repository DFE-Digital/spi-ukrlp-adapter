using System.Linq;
using System.Xml.Linq;
using AutoFixture;
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi.UnitTests
{
    public class WhenBuildingMessageToGetSpecificUkprn
    {
        private static readonly XNamespace soapNs = "http://schemas.xmlsoap.org/soap/envelope/";
        private static readonly XNamespace ukrlpNs = "http://ukrlp.co.uk.server.ws.v3";

        private string _stakeholderId;
        private long _ukprn;
        private UkrlpSoapMessageBuilder _builder;

        [SetUp]
        public void Arrange()
        {
            var fixture = new Fixture();

            _stakeholderId = fixture.Create<string>();
            _ukprn = fixture.Create<long>();

            _builder = new UkrlpSoapMessageBuilder(_stakeholderId);
        }

        [Test]
        public void ThenItShouldReturnSoapMesage()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprn(_ukprn);

            var envelope = XElement.Parse(actual);
            Assert.AreEqual("Envelope", envelope.Name.LocalName);
            Assert.AreEqual(soapNs.NamespaceName, envelope.Name.NamespaceName);

            Assert.IsNotNull(envelope.Elements().SingleOrDefault(e =>
                e.Name.LocalName == "Header" && e.Name.NamespaceName == soapNs.NamespaceName));

            Assert.IsNotNull(envelope.Elements().SingleOrDefault(e =>
                e.Name.LocalName == "Body" && e.Name.NamespaceName == soapNs.NamespaceName));
        }

        [Test]
        public void ThenItShouldHaveAProviderQueryRequestInTheSoapBody()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprn(_ukprn);

            var body = XElement.Parse(actual).GetElementByLocalName("Body");
            Assert.IsNotNull(body.Elements().SingleOrDefault(e =>
                e.Name.LocalName == "ProviderQueryRequest" &&
                e.Name.NamespaceName == ukrlpNs.NamespaceName));
        }

        [Test]
        public void ThenItShouldHaveAQueryIdInRequest()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprn(_ukprn);

            var request = XElement.Parse(actual).GetElementByLocalName("Body").GetElementByLocalName("ProviderQueryRequest");
            var queryId = request.GetElementByLocalName("QueryId");
            Assert.IsNotNull(queryId);
        }

        [Test]
        public void ThenItShouldHaveAStakeholderIdInSelectionCriteria()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprn(_ukprn);

            var selectionCriteria = XElement.Parse(actual)
                .GetElementByLocalName("Body")
                .GetElementByLocalName("ProviderQueryRequest")
                .GetElementByLocalName("SelectionCriteria");
            var stakeholderId = selectionCriteria.GetElementByLocalName("StakeholderId");
            Assert.IsNotNull(stakeholderId);
            Assert.AreEqual(_stakeholderId, stakeholderId.Value);
        }

        [Test]
        public void ThenItShouldHaveASelectionCriteriaForUkprn()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprn(_ukprn);

            var selectionCriteria = XElement.Parse(actual)
                .GetElementByLocalName("Body")
                .GetElementByLocalName("ProviderQueryRequest")
                .GetElementByLocalName("SelectionCriteria");

            var ukprnList = selectionCriteria.GetElementByLocalName("UnitedKingdomProviderReferenceNumberList");
            Assert.IsNotNull(ukprnList);

            var ukprn = ukprnList.GetElementByLocalName("UnitedKingdomProviderReferenceNumber");
            Assert.IsNotNull(ukprn);
            Assert.AreEqual(_ukprn.ToString(), ukprn.Value);
        }
    }
}