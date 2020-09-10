using System.Linq;
using System.Xml.Linq;
using AutoFixture;
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi.UnitTests
{
    public class WhenBuildingMessageToGetSpecificUkprns
    {
        private static readonly XNamespace soapNs = "http://schemas.xmlsoap.org/soap/envelope/";
        private static readonly XNamespace ukrlpNs = "http://ukrlp.co.uk.server.ws.v3";

        private Fixture _fixture;
        private string _stakeholderId;
        private long _ukprn;
        private UkrlpSoapMessageBuilder _builder;

        [SetUp]
        public void Arrange()
        {
            _fixture = new Fixture();

            _stakeholderId = _fixture.Create<string>();
            _ukprn = _fixture.Create<long>();

            _builder = new UkrlpSoapMessageBuilder(_stakeholderId);
        }

        [Test]
        public void ThenItShouldReturnSoapMesage()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprns(new[]{_ukprn});

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
            var actual = _builder.BuildMessageToGetSpecificUkprns(new[]{_ukprn});

            var body = XElement.Parse(actual).GetElementByLocalName("Body");
            Assert.IsNotNull(body.Elements().SingleOrDefault(e =>
                e.Name.LocalName == "ProviderQueryRequest" &&
                e.Name.NamespaceName == ukrlpNs.NamespaceName));
        }

        [Test]
        public void ThenItShouldHaveAQueryIdInRequest()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprns(new[]{_ukprn});

            var request = XElement.Parse(actual).GetElementByLocalName("Body").GetElementByLocalName("ProviderQueryRequest");
            var queryId = request.GetElementByLocalName("QueryId");
            Assert.IsNotNull(queryId);
        }

        [Test]
        public void ThenItShouldHaveAStakeholderIdInSelectionCriteria()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprns(new[]{_ukprn});

            var selectionCriteria = XElement.Parse(actual)
                .GetElementByLocalName("Body")
                .GetElementByLocalName("ProviderQueryRequest")
                .GetElementByLocalName("SelectionCriteria");
            var stakeholderId = selectionCriteria.GetElementByLocalName("StakeholderId");
            Assert.IsNotNull(stakeholderId);
            Assert.AreEqual(_stakeholderId, stakeholderId.Value);
        }

        [Test]
        public void ThenItShouldHaveASelectionCriteriaForUkprns()
        {
            var ukprn1 = _fixture.Create<long>();
            var ukprn2 = _fixture.Create<long>();
            
            var actual = _builder.BuildMessageToGetSpecificUkprns(new[]{ukprn1, ukprn2});

            var selectionCriteria = XElement.Parse(actual)
                .GetElementByLocalName("Body")
                .GetElementByLocalName("ProviderQueryRequest")
                .GetElementByLocalName("SelectionCriteria");

            var ukprnList = selectionCriteria.GetElementByLocalName("UnitedKingdomProviderReferenceNumberList");
            Assert.IsNotNull(ukprnList);

            var ukprns = ukprnList.GetElementsByLocalName("UnitedKingdomProviderReferenceNumber");
            Assert.IsNotNull(ukprns);
            Assert.AreEqual(2, ukprns.Length);
            Assert.AreEqual(ukprn1.ToString(), ukprns[0].Value);
            Assert.AreEqual(ukprn2.ToString(), ukprns[1].Value);
        }

        [Test]
        public void ThenItShouldHaveASelectionCriteriaForDefaultStatus()
        {
            var actual = _builder.BuildMessageToGetSpecificUkprns(new[]{_ukprn});

            var selectionCriteria = XElement.Parse(actual)
                .GetElementByLocalName("Body")
                .GetElementByLocalName("ProviderQueryRequest")
                .GetElementByLocalName("SelectionCriteria");

            var status = selectionCriteria.GetElementByLocalName("ProviderStatus");
            Assert.IsNotNull(status);
            Assert.AreEqual("A", status.Value);
        }

        [TestCase("A")]
        [TestCase("V")]
        [TestCase("PD1")]
        [TestCase("PD2")]
        public void ThenItShouldHaveASelectionCriteriaForSpecifiedStatus(string providerStatus)
        {
            var actual = _builder.BuildMessageToGetSpecificUkprns(new[]{_ukprn}, providerStatus);

            var selectionCriteria = XElement.Parse(actual)
                .GetElementByLocalName("Body")
                .GetElementByLocalName("ProviderQueryRequest")
                .GetElementByLocalName("SelectionCriteria");

            var status = selectionCriteria.GetElementByLocalName("ProviderStatus");
            Assert.IsNotNull(status);
            Assert.AreEqual(providerStatus, status.Value);
        }
    }
}