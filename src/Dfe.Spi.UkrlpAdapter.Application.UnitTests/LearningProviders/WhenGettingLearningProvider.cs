using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture;
using AutoFixture.NUnit3;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Common.UnitTesting.Fixtures;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Application.LearningProviders;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
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
        private Mock<IProviderRepository> _providerRepository;
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
            _fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
                .ForEach(b => _fixture.Behaviors.Remove(b));
            _fixture.Behaviors.Add(new OmitOnRecursionBehavior()); // recursionDepth

            _ukrlpApiClientMock = new Mock<IUkrlpApiClient>();
            
            _providerRepository = new Mock<IProviderRepository>();

            _mapperMock = new Mock<IMapper>();

            _loggerMock = new Mock<ILoggerWrapper>();

            _manager = new LearningProviderManager(
                _ukrlpApiClientMock.Object, 
                _providerRepository.Object, 
                _mapperMock.Object, 
                _loggerMock.Object);

            _cancellationToken = new CancellationToken();
        }

        [Test]
        public async Task ThenItShouldGetProviderFromApiIfReadFromLive()
        {
            var ukprn = _fixture.Create<long>();

            await _manager.GetLearningProviderAsync(ukprn.ToString(), null, true, _cancellationToken);

            _ukrlpApiClientMock.Verify(c => c.GetProviderAsync(ukprn, _cancellationToken),
                Times.Once);
            _providerRepository.Verify(c => c.GetProviderAsync(ukprn, _cancellationToken),
                Times.Never);
        }

        [Test]
        public async Task ThenItShouldReturnNullIfProviderNotFoundOnApiIfReadFromLive()
        {
            var ukprn = _fixture.Create<long>();
            
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync((Provider) null);

            var actual = await _manager.GetLearningProviderAsync(ukprn.ToString(), null, true, _cancellationToken);

            Assert.IsNull(actual);
        }

        [Test]
        public async Task ThenItShouldGetProviderFromCacheIfNotReadFromLive()
        {
            var ukprn = _fixture.Create<long>();

            await _manager.GetLearningProviderAsync(ukprn.ToString(), null, false, _cancellationToken);

            _providerRepository.Verify(c => c.GetProviderAsync(ukprn, _cancellationToken),
                Times.Once);
            _ukrlpApiClientMock.Verify(c => c.GetProviderAsync(ukprn, _cancellationToken),
                Times.Never);
        }

        [Test]
        public async Task ThenItShouldReturnNullIfProviderNotFoundOnCacheIfNotReadFromLive()
        {
            var ukprn = _fixture.Create<long>();
            
            _providerRepository.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync((Provider) null);

            var actual = await _manager.GetLearningProviderAsync(ukprn.ToString(), null, false, _cancellationToken);

            Assert.IsNull(actual);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ThenItShouldThrowExceptionIfIdIsNotNumeric(bool readFromLive)
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProviderAsync("NotANumber", null, readFromLive, _cancellationToken));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void ThenItShouldThrowExceptionIfIdIsNot8Digits(bool readFromLive)
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProviderAsync("123456789", null, readFromLive, _cancellationToken));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task ThenItShouldMapProviderToLearningProvider(bool readFromLive)
        {
            var establishment = _fixture.Create<Provider>();
            var ukprn = _fixture.Create<long>();
            
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync(establishment);
            _providerRepository.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync(establishment);

            await _manager.GetLearningProviderAsync(ukprn.ToString(), null, readFromLive, _cancellationToken);

            _mapperMock.Verify(m => m.MapAsync<LearningProvider>(establishment, _cancellationToken),
                Times.Once);
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task ThenItShouldReturnMappedLearningProvider(bool readFromLive)
        {
            var learningProvider = _fixture.Create<LearningProvider>();
            var ukprn = _fixture.Create<long>();
            
            _ukrlpApiClientMock.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync(new Provider());
            _providerRepository.Setup(c => c.GetProviderAsync(ukprn, _cancellationToken))
                .ReturnsAsync(new Provider());
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync(learningProvider);

            var actual = await _manager.GetLearningProviderAsync(ukprn.ToString(), null, readFromLive, _cancellationToken);

            Assert.AreSame(learningProvider, actual);
        }
    }
}