using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Common.UnitTesting.Fixtures;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Application.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Events;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Moq;
using NUnit.Framework;

namespace Dfe.Spi.GiasAdapter.Application.UnitTests.Cache
{
    public class WhenProcessingBatchOfEstablishments
    {
        private Mock<IStateRepository> _stateRepositoryMock;
        private Mock<IUkrlpApiClient> _ukrlpApiClientMock;
        private Mock<IProviderRepository> _providerRepositoryMock;
        private Mock<IMapper> _mapperMock;
        private Mock<IEventPublisher> _eventPublisherMock;
        private Mock<IProviderProcessingQueue> _providerProcessingQueueMock;
        private Mock<ILoggerWrapper> _loggerMock;
        private CacheManager _manager;
        private CancellationToken _cancellationToken;

        [SetUp]
        public void Arrange()
        {
            _stateRepositoryMock = new Mock<IStateRepository>();
            
            _ukrlpApiClientMock = new Mock<IUkrlpApiClient>();

            _mapperMock = new Mock<IMapper>();
            _mapperMock.Setup(m=>m.MapAsync<LearningProvider>(It.IsAny<Provider>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Provider provider, CancellationToken cancellationToken) => new LearningProvider
                {
                    Name = provider.ProviderName,
                });

            _eventPublisherMock = new Mock<IEventPublisher>();

            _providerProcessingQueueMock = new Mock<IProviderProcessingQueue>();
            
            _providerRepositoryMock = new Mock<IProviderRepository>();
            _providerRepositoryMock.Setup(r => r.GetProviderAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PointInTimeProvider) null);
            _providerRepositoryMock.Setup(r =>
                    r.GetProviderFromStagingAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((long ukprn, DateTime pointInTime, CancellationToken cancellationToken) => new PointInTimeProvider
                {
                    UnitedKingdomProviderReferenceNumber = ukprn,
                    ProviderName = ukprn.ToString(),
                });

            _loggerMock = new Mock<ILoggerWrapper>();

            _manager = new CacheManager(
                _stateRepositoryMock.Object,
                _ukrlpApiClientMock.Object,
                _providerRepositoryMock.Object,
                _mapperMock.Object,
                _eventPublisherMock.Object,
                _providerProcessingQueueMock.Object,
                _loggerMock.Object);

            _cancellationToken = new CancellationToken();
        }

        [Test]
        public async Task ThenItShouldProcessEveryUrn()
        {
            var ukprns = new[] {100001L, 100002L};
            var pointInTime = DateTime.UtcNow.Date;

            await _manager.ProcessBatchOfProviders(ukprns, pointInTime, _cancellationToken);

            _providerRepositoryMock.Verify(
                r => r.GetProviderAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            _providerRepositoryMock.Verify(r => r.GetProviderAsync(ukprns[0], _cancellationToken),
                Times.Once);
            _providerRepositoryMock.Verify(r => r.GetProviderAsync(ukprns[1], _cancellationToken),
                Times.Once);

            _providerRepositoryMock.Verify(
                r => r.GetProviderFromStagingAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            _providerRepositoryMock.Verify(r => r.GetProviderFromStagingAsync(ukprns[0], pointInTime, _cancellationToken),
                Times.Once);
            _providerRepositoryMock.Verify(r => r.GetProviderFromStagingAsync(ukprns[1], pointInTime, _cancellationToken),
                Times.Once);

            _providerRepositoryMock.Verify(
                r => r.StoreAsync(It.IsAny<PointInTimeProvider[]>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            _providerRepositoryMock.Verify(
                r => r.StoreAsync(It.Is<PointInTimeProvider[]>(e => e.First().UnitedKingdomProviderReferenceNumber == ukprns[0]), _cancellationToken),
                Times.Once);
            _providerRepositoryMock.Verify(
                r => r.StoreAsync(It.Is<PointInTimeProvider[]>(e => e.First().UnitedKingdomProviderReferenceNumber == ukprns[1]), _cancellationToken),
                Times.Once);

            _mapperMock.Verify(
                m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            _mapperMock.Verify(
                m => m.MapAsync<LearningProvider>(It.Is<Provider>(e => e.UnitedKingdomProviderReferenceNumber == ukprns[0]), It.IsAny<CancellationToken>()),
                Times.Once);
            _mapperMock.Verify(
                m => m.MapAsync<LearningProvider>(It.Is<Provider>(e => e.UnitedKingdomProviderReferenceNumber == ukprns[1]), It.IsAny<CancellationToken>()),
                Times.Once);

            _eventPublisherMock.Verify(
                p => p.PublishLearningProviderCreatedAsync(It.IsAny<LearningProvider>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Exactly(2));
        }

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldPublishCreatedEventIfNoPrevious(long ukprn, DateTime pointInTime, LearningProvider learningProvider)
        {
            _providerRepositoryMock.Setup(r => r.GetProviderAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PointInTimeProvider) null);
            _providerRepositoryMock.Setup(r =>
                    r.GetProviderFromStagingAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PointInTimeProvider
                {
                    UnitedKingdomProviderReferenceNumber = ukprn,
                    ProviderName = ukprn.ToString(),
                    PointInTime = pointInTime,
                }); 
            _mapperMock.Setup(m=>m.MapAsync<LearningProvider>(It.IsAny<Provider>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(learningProvider);
            
            await _manager.ProcessBatchOfProviders(new[]{ukprn}, pointInTime, _cancellationToken);

            _eventPublisherMock.Verify(
                p => p.PublishLearningProviderCreatedAsync(learningProvider, pointInTime, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldPublishUpdatedEventIfHasChangedSincePrevious(long ukprn, DateTime pointInTime, LearningProvider learningProvider)
        {
            _providerRepositoryMock.Setup(r => r.GetProviderAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PointInTimeProvider
                {
                    UnitedKingdomProviderReferenceNumber = ukprn,
                    ProviderName = "old name",
                });
            _providerRepositoryMock.Setup(r =>
                    r.GetProviderFromStagingAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PointInTimeProvider
                {
                    UnitedKingdomProviderReferenceNumber = ukprn,
                    ProviderName = ukprn.ToString(),
                    PointInTime = pointInTime,
                }); 
            _mapperMock.Setup(m=>m.MapAsync<LearningProvider>(It.IsAny<Provider>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(learningProvider);
            
            await _manager.ProcessBatchOfProviders(new[]{ukprn}, pointInTime, _cancellationToken);

            _eventPublisherMock.Verify(
                p => p.PublishLearningProviderUpdatedAsync(learningProvider, pointInTime, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldNotPublishAnyEventIfHasNotChangedSincePrevious(long urn, DateTime pointInTime)
        {
            _providerRepositoryMock.Setup(r => r.GetProviderAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PointInTimeProvider
                {
                    UnitedKingdomProviderReferenceNumber = urn,
                    ProviderName = urn.ToString(),
                    PointInTime = pointInTime,
                });
            _providerRepositoryMock.Setup(r =>
                    r.GetProviderFromStagingAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PointInTimeProvider
                {
                    UnitedKingdomProviderReferenceNumber = urn,
                    ProviderName = urn.ToString(),
                    PointInTime = pointInTime,
                }); 
            
            await _manager.ProcessBatchOfProviders(new[]{urn}, pointInTime, _cancellationToken);

            _eventPublisherMock.Verify(
                p => p.PublishLearningProviderCreatedAsync(It.IsAny<LearningProvider>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Never);
            _eventPublisherMock.Verify(
                p => p.PublishLearningProviderUpdatedAsync(It.IsAny<LearningProvider>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}