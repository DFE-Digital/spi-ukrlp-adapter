using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Http.Server;
using Dfe.Spi.Common.Http.Server.Definitions;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Common.Models;
using Dfe.Spi.Common.UnitTesting.Fixtures;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Application.LearningProviders;
using Dfe.Spi.UkrlpAdapter.Functions.LearningProviders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Functions.UnitTests.LearningProviders
{
    public class WhenGettingLearningProviders
    {
        private Mock<ILearningProviderManager> _learningProviderManagerMock;
        private Mock<IHttpSpiExecutionContextManager> _httpSpiExecutionContextManager;
        private Mock<ILoggerWrapper> _loggerMock;
        private GetLearningProviders _function;
        private CancellationToken _cancellationToken;

        [SetUp]
        public void Arrange()
        {
            _learningProviderManagerMock = new Mock<ILearningProviderManager>();
            _learningProviderManagerMock.Setup(p => p.GetLearningProvidersAsync(
                    It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LearningProvider[0]);

            _httpSpiExecutionContextManager = new Mock<IHttpSpiExecutionContextManager>();

            _loggerMock = new Mock<ILoggerWrapper>();

            _function = new GetLearningProviders(
                _learningProviderManagerMock.Object,
                _httpSpiExecutionContextManager.Object,
                _loggerMock.Object);

            _cancellationToken = new CancellationToken();
        }

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldReturnLearningProviders(LearningProvider[] providers)
        {
            _learningProviderManagerMock.Setup(p => p.GetLearningProvidersAsync(
                    It.IsAny<string[]>(), It.IsAny<string[]>(), It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(providers);

            var actual = await _function.RunAsync(GetHttpRequest(providers), _cancellationToken);

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<FormattedJsonResult>(actual);
            Assert.AreEqual(HttpStatusCode.OK, ((FormattedJsonResult) actual).StatusCode);

            var actualProviders = ((FormattedJsonResult) actual).Value as LearningProvider[];
            Assert.IsNotNull(actualProviders);
            Assert.AreSame(providers, actualProviders);
        }


        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldCallManagerWithSpecifiedIdentifiersAndFieldsAndLiveOption(LearningProvider[] providers, string[] fields, bool readFromLive, DateTime? pointInTime)
        {
            await _function.RunAsync(GetHttpRequest(providers, fields, readFromLive, pointInTime), _cancellationToken);

            _learningProviderManagerMock.Verify(p => p.GetLearningProvidersAsync(
                    It.Is<string[]>(ids => ids.Length == providers.Length),
                    fields,
                    readFromLive,
                    pointInTime,
                    _cancellationToken),
                Times.Once);
        }

        [Test]
        public async Task ThenItShouldReturnBadRequestIfBodyNotJson()
        {
            var httpRequest = new DefaultHttpRequest(new DefaultHttpContext())
            {
                Body = new MemoryStream(Encoding.UTF8.GetBytes("not-json")),
            };
            
            var actual = await _function.RunAsync(httpRequest, _cancellationToken);
            
            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<HttpErrorBodyResult>(actual);
            Assert.AreEqual(400, ((HttpErrorBodyResult) actual).StatusCode);

            var errorBody = (HttpErrorBody)((JsonResult) actual).Value;
            Assert.AreEqual(HttpStatusCode.BadRequest, errorBody.StatusCode);
            Assert.AreEqual(Errors.GetLearningProvidersMalformedRequest.Code, errorBody.ErrorIdentifier);
            Assert.AreEqual(Errors.GetLearningProvidersMalformedRequest.Message, errorBody.Message);
        }

        [Test]
        public async Task ThenItShouldReturnBadRequestIfIdentifiersIsNull()
        {
            var getLearningProvidersRequest = GetDefaultGetLearningProvidersRequest();
            getLearningProvidersRequest.Identifiers = null;
            var httpRequest = GetHttpRequestFromGetLearningProvidersRequest(getLearningProvidersRequest);
            
            var actual = await _function.RunAsync(httpRequest, _cancellationToken);
            
            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<HttpSchemaValidationErrorBodyResult>(actual);
            Assert.AreEqual(400, ((HttpSchemaValidationErrorBodyResult) actual).StatusCode);

            var errorBody = (HttpDetailedErrorBody)((JsonResult) actual).Value;
            Assert.AreEqual(HttpStatusCode.BadRequest, errorBody.StatusCode);
            Assert.AreEqual(Errors.GetLearningProvidersSchemaValidation.Code, errorBody.ErrorIdentifier);
            Assert.IsNotNull(errorBody.Details);
            Assert.AreEqual(1, errorBody.Details.Length);
        }

        [Test]
        public async Task ThenItShouldReturnBadRequestIfIdentifiersHasNoItems()
        {
            var httpRequest = GetHttpRequest(new string[0]);
            
            var actual = await _function.RunAsync(httpRequest, _cancellationToken);
            
            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<HttpSchemaValidationErrorBodyResult>(actual);
            Assert.AreEqual(400, ((HttpSchemaValidationErrorBodyResult) actual).StatusCode);

            var errorBody = (HttpDetailedErrorBody)((JsonResult) actual).Value;
            Assert.AreEqual(HttpStatusCode.BadRequest, errorBody.StatusCode);
            Assert.AreEqual(Errors.GetLearningProvidersSchemaValidation.Code, errorBody.ErrorIdentifier);
            Assert.IsNotNull(errorBody.Details);
            Assert.AreEqual(1, errorBody.Details.Length);
        }


        private HttpRequest GetHttpRequest(LearningProvider[] providers, string[] fields = null, bool? readFromLive = null, DateTime? pointInTime = null)
        {
            var identifiers = providers.Select(p => p.Ukprn.ToString()).ToArray();
            return GetHttpRequest(identifiers, fields, readFromLive, pointInTime);
        }

        private HttpRequest GetHttpRequest(string[] identifiers = null, string[] fields = null, bool? readFromLive = null, DateTime? pointInTime = null)
        {
            return GetHttpRequestFromGetLearningProvidersRequest(
                GetDefaultGetLearningProvidersRequest(identifiers, fields, readFromLive, pointInTime));
        }

        private HttpRequest GetHttpRequestFromGetLearningProvidersRequest(GetLearningProvidersRequest getLearningProvidersRequest)
        {
            var json = JsonConvert.SerializeObject(getLearningProvidersRequest,
                new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                });
            return new DefaultHttpRequest(new DefaultHttpContext())
            {
                Body = new MemoryStream(Encoding.UTF8.GetBytes(json)),
            };
        }

        private GetLearningProvidersRequest GetDefaultGetLearningProvidersRequest(string[] identifiers = null, string[] fields = null, bool? readFromLive = null, DateTime? pointInTime = null)
        {
            return new GetLearningProvidersRequest
            {
                Identifiers = identifiers ?? new[] {"12345678"},
                Fields = fields,
                Live = readFromLive.HasValue && readFromLive.Value,
                PointInTime = pointInTime,
            };
        }
    }
}