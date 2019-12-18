using System;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Common.UnitTesting.Fixtures;
using Dfe.Spi.Models;
using Dfe.Spi.UkrlpAdapter.Application.LearningProviders;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Moq;
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Application.UnitTests.LearningProviders
{
    public class WhenGettingLearningProvider
    {
        private Mock<IUkrlpApiClient> _ukrlpApiClientMock;
        private Mock<IMapper> _mapperMock;
        private Mock<ILoggerWrapper> _loggerMock;
        private LearningProviderManager _manager;
        private CancellationToken _cancellationToken;

        [SetUp]
        public void Arrange()
        {
            _ukrlpApiClientMock = new Mock<IUkrlpApiClient>();

            _mapperMock = new Mock<IMapper>();
            
            _loggerMock = new Mock<ILoggerWrapper>();

            _manager = new LearningProviderManager(_ukrlpApiClientMock.Object, _mapperMock.Object, _loggerMock.Object);

            _cancellationToken = new CancellationToken();
        }

        [Test, AutoData]
        public async Task ThenItShouldGetProviderFromApi(int urn)
        {
            await _manager.GetLearningProviderAsync(urn.ToString(), _cancellationToken);

            _ukrlpApiClientMock.Verify(c => c.GetProviderAsync(urn, _cancellationToken),
                Times.Once);
        }

        [Test]
        public void ThenItShouldThrowExceptionIfIdIsNotNumeric()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProviderAsync("NotANumber", _cancellationToken));
        }

        [Test, AutoData]
        public async Task ThenItShouldMapProviderToLearningProvider(int urn, Provider establishment)
        {
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(urn, _cancellationToken))
                .ReturnsAsync(establishment);

            await _manager.GetLearningProviderAsync(urn.ToString(), _cancellationToken);

            _mapperMock.Verify(m => m.MapAsync<LearningProvider>(establishment, _cancellationToken),
                Times.Once);
        }

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldReturnMappedLearningProvider(int urn, LearningProvider learningProvider)
        {
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(urn, _cancellationToken))
                .ReturnsAsync(new Provider());
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync(learningProvider);

            var actual = await _manager.GetLearningProviderAsync(urn.ToString(), _cancellationToken);

            Assert.AreSame(learningProvider, actual);
        }

        [Test, AutoData]
        public async Task ThenItShouldReturnNullIfProviderNotFoundOnApi(int urn)
        {
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(urn, _cancellationToken))
                .ReturnsAsync((Provider) null);

            var actual = await _manager.GetLearningProviderAsync(urn.ToString(), _cancellationToken);

            Assert.IsNull(actual);
        }
    }
}