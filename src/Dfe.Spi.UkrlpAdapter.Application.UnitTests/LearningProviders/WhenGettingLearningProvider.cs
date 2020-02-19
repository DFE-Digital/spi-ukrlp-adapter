using System;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.NUnit3;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Common.UnitTesting.Fixtures;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Application.LearningProviders;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Moq;
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Application.UnitTests.LearningProviders
{
    public class WhenGettingLearningProvider
    {
        private Fixture _fixture;
        private Mock<IUkrlpApiClient> _ukrlpApiClientMock;
        private Mock<IMapper> _mapperMock;
        private Mock<ILoggerWrapper> _loggerMock;
        private LearningProviderManager _manager;
        private CancellationToken _cancellationToken;

        [SetUp]
        public void Arrange()
        {
            var random = new Random();
            _fixture = new Fixture();
            _fixture.Register(() => (long) random.Next(10000000, 99999999));

            _ukrlpApiClientMock = new Mock<IUkrlpApiClient>();

            _mapperMock = new Mock<IMapper>();

            _loggerMock = new Mock<ILoggerWrapper>();

            _manager = new LearningProviderManager(_ukrlpApiClientMock.Object, _mapperMock.Object, _loggerMock.Object);

            _cancellationToken = new CancellationToken();
        }

        [Test]
        public async Task ThenItShouldGetProviderFromApi()
        {
            var ukprn = _fixture.Create<long>();

            await _manager.GetLearningProviderAsync(ukprn.ToString(), null, _cancellationToken);

            _ukrlpApiClientMock.Verify(c => c.GetProviderAsync(ukprn, _cancellationToken),
                Times.Once);
        }

        [Test]
        public void ThenItShouldThrowExceptionIfIdIsNotNumeric()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProviderAsync("NotANumber", null, _cancellationToken));
        }

        [Test]
        public void ThenItShouldThrowExceptionIfIdIsNot8Digits()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProviderAsync("123456789", null, _cancellationToken));
        }

        [Test, AutoData]
        public async Task ThenItShouldMapProviderToLearningProvider(Provider establishment)
        {
            var ukprn = _fixture.Create<long>();
            
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync(establishment);

            await _manager.GetLearningProviderAsync(ukprn.ToString(), null, _cancellationToken);

            _mapperMock.Verify(m => m.MapAsync<LearningProvider>(establishment, _cancellationToken),
                Times.Once);
        }

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldReturnMappedLearningProvider(LearningProvider learningProvider)
        {
            var ukprn = _fixture.Create<long>();
            
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync(new Provider());
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync(learningProvider);

            var actual = await _manager.GetLearningProviderAsync(ukprn.ToString(), null, _cancellationToken);

            Assert.AreSame(learningProvider, actual);
        }

        [Test, AutoData]
        public async Task ThenItShouldReturnNullIfProviderNotFoundOnApi()
        {
            var ukprn = _fixture.Create<long>();
            
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync((Provider) null);

            var actual = await _manager.GetLearningProviderAsync(ukprn.ToString(), null, _cancellationToken);

            Assert.IsNull(actual);
        }
    }
}