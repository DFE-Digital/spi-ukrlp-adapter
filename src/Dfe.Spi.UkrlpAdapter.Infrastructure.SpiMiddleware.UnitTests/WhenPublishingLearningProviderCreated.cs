using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Common.UnitTesting.Fixtures;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using RestSharp;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.SpiMiddleware.UnitTests
{
    public class WhenPublishingLearningProviderCreated
    {
        private AuthenticationConfiguration _authenticationConfiguration;
        private Mock<IRestClient> _restClientMock;
        private Mock<ILoggerWrapper> _loggerMock;
        private MiddlewareEventPublisher _publisher;
        private CancellationToken _cancellationToken;
        private MiddlewareConfiguration _configuration;

        [SetUp]
        public void Arrange()
        {
            _authenticationConfiguration = new AuthenticationConfiguration()
            {
                ClientId = "some client id",
                ClientSecret = "some secret",
                Resource = "http://some.fake.url/abc123",
                TokenEndpoint = "https://somecorp.local/tokens",
            };

            _restClientMock = new Mock<IRestClient>();
            _restClientMock.Setup(c => c.ExecuteTaskAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestResponse
                {
                    StatusCode = HttpStatusCode.Accepted,
                    ResponseStatus = ResponseStatus.Completed,
                });

            _loggerMock = new Mock<ILoggerWrapper>();

            _configuration = new MiddlewareConfiguration
            {
                BaseUrl = "https://middleware.unit.tests",
            };

            _publisher = new MiddlewareEventPublisher(
                _authenticationConfiguration,
                _configuration,
                _restClientMock.Object,
                _loggerMock.Object);

            _cancellationToken = new CancellationToken();
        }

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldPostProviderToProviderCreatedEndpoint(LearningProvider learningProvider)
        {
            await _publisher.PublishLearningProviderCreatedAsync(learningProvider, _cancellationToken);

            var expectedBody = JsonConvert.SerializeObject(learningProvider);
            _restClientMock.Verify(c=>c.ExecuteTaskAsync(It.Is<RestRequest>(req=>
                req.Method == Method.POST &&
                req.Resource == "learning-provider-created" &&
                req.Parameters.SingleOrDefault(p=>p.Type == ParameterType.RequestBody) != null &&
                (string)req.Parameters.Single(p=>p.Type == ParameterType.RequestBody).Value == expectedBody), _cancellationToken), Times.Once);
        }

        [Test]
        public void ThenItShouldThrowExceptionIfMiddlewareReturnsNonSuccess()
        {
            _restClientMock.Setup(c => c.ExecuteTaskAsync(It.IsAny<IRestRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new RestResponse
                {
                    StatusCode = HttpStatusCode.InternalServerError,
                    Content = "Some error",
                    ResponseStatus = ResponseStatus.Completed,
                });
            
            var actual = Assert.ThrowsAsync<MiddlewareException>(async () =>
                await _publisher.PublishLearningProviderCreatedAsync(new LearningProvider(), _cancellationToken));
            Assert.AreEqual(HttpStatusCode.InternalServerError, actual.StatusCode);
            Assert.AreEqual("Some error", actual.Details);
        }
    }
}