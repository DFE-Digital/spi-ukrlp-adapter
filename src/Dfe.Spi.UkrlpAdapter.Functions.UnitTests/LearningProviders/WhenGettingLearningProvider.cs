using System;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
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
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Functions.UnitTests.LearningProviders
{
    public class WhenGettingLearningProvider
    {
        private Mock<ILearningProviderManager> _learningProviderManagerMock;
        private Mock<IHttpSpiExecutionContextManager> _httpSpiExecutionContextManager;
        private Mock<ILoggerWrapper> _loggerMock;
        private GetLearningProvider _function;
        private CancellationToken _cancellationToken;

        [SetUp]
        public void Arrange()
        {
            _learningProviderManagerMock = new Mock<ILearningProviderManager>();

            _httpSpiExecutionContextManager = new Mock<IHttpSpiExecutionContextManager>();

            _loggerMock = new Mock<ILoggerWrapper>();

            _function = new GetLearningProvider(
                _learningProviderManagerMock.Object,
                _httpSpiExecutionContextManager.Object,
                _loggerMock.Object);

            _cancellationToken = new CancellationToken();
        }

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldReturnLearningProviderIfFound(int urn, LearningProvider provider)
        {
            _learningProviderManagerMock.Setup(x =>
                    x.GetLearningProviderAsync(It.IsAny<string>(), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(provider);

            var actual = await _function.Run(new DefaultHttpRequest(new DefaultHttpContext()), urn.ToString(),
                _cancellationToken);

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<JsonResult>(actual);
            Assert.AreSame(provider, ((JsonResult) actual).Value);
        }

        [Test]
        public async Task ThenItShouldReturnNotFoundResultIfNotFound()
        {
            _learningProviderManagerMock.Setup(x =>
                    x.GetLearningProviderAsync(It.IsAny<string>(), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((LearningProvider) null);

            var actual = await _function.Run(new DefaultHttpRequest(new DefaultHttpContext()), "123",
                _cancellationToken);

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<NotFoundResult>(actual);
        }

        [Test, AutoData]
        public async Task ThenItShouldReturnBadRequestIfArgumentExceptionThrown(string message)
        {
            _learningProviderManagerMock.Setup(x =>
                    x.GetLearningProviderAsync(It.IsAny<string>(), null, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException(message));

            var actual = await _function.Run(new DefaultHttpRequest(new DefaultHttpContext()), "123",
                _cancellationToken);

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<HttpErrorBodyResult>(actual);
            Assert.IsInstanceOf<HttpErrorBody>(((HttpErrorBodyResult) actual).Value);
            Assert.AreSame(message, ((HttpErrorBody)((HttpErrorBodyResult) actual).Value).Message);
            Assert.AreSame("SPI-UKRLP-PROV01", ((HttpErrorBody)((HttpErrorBodyResult) actual).Value).ErrorIdentifier);
        }
    }
}