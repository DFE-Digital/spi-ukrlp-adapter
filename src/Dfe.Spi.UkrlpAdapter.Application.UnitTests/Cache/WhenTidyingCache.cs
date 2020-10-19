using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Application.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.Events;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Moq;
using NUnit.Framework;

namespace Dfe.Spi.GiasAdapter.Application.UnitTests.Cache
{
    public class WhenTidyingCache
    {
        private Mock<IStateRepository> _stateRepositoryMock;
        private Mock<IUkrlpApiClient> _ukrlpApiClientMock;
        private Mock<IProviderRepository> _providerRepositoryMock;
        private Mock<IMapper> _mapperMock;
        private Mock<IEventPublisher> _eventPublisherMock;
        private Mock<IProviderProcessingQueue> _providerProcessingQueueMock;
        private CacheConfiguration _configuration;
        private Mock<ILoggerWrapper> _loggerMock;
        private CacheManager _manager;
        private CancellationToken _cancellationToken;

        [SetUp]
        public void Arrange()
        {
            _stateRepositoryMock = new Mock<IStateRepository>();
            _stateRepositoryMock.Setup(r => r.GetLastStagingDateClearedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTime.Today.AddDays(-15));

            _ukrlpApiClientMock = new Mock<IUkrlpApiClient>();

            _mapperMock = new Mock<IMapper>();

            _eventPublisherMock = new Mock<IEventPublisher>();

            _providerProcessingQueueMock = new Mock<IProviderProcessingQueue>();

            _providerRepositoryMock = new Mock<IProviderRepository>();

            _configuration = new CacheConfiguration
            {
                NumberOfDaysToRetainStagingData = 14,
            };

            _loggerMock = new Mock<ILoggerWrapper>();

            _manager = new CacheManager(
                _stateRepositoryMock.Object,
                _ukrlpApiClientMock.Object,
                _providerRepositoryMock.Object,
                _mapperMock.Object,
                _eventPublisherMock.Object,
                _providerProcessingQueueMock.Object,
                _configuration,
                _loggerMock.Object);

            _cancellationToken = new CancellationToken();
        }

        [Test]
        public async Task ThenItShouldGetLastClearedDateFromRepo()
        {
            await _manager.TidyCacheAsync(_cancellationToken);

            _stateRepositoryMock.Verify(r => r.GetLastStagingDateClearedAsync(_cancellationToken),
                Times.Once);
        }

        [TestCase(15, 1)]
        [TestCase(16, 2)]
        [TestCase(17, 3)]
        [TestCase(18, 4)]
        public async Task ThenItShouldSetLastClearedStateForEachDayBetweenLastClearedAndRetentionDate(
            int numberOfDaysAgoOfLastCleared,
            int expectedNumberOfDaysToClearInRun)
        {
            _stateRepositoryMock.Setup(r => r.GetLastStagingDateClearedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTime.Today.AddDays(-numberOfDaysAgoOfLastCleared));

            await _manager.TidyCacheAsync(_cancellationToken);

            _stateRepositoryMock.Verify(r => r.SetLastStagingDateClearedAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Exactly(expectedNumberOfDaysToClearInRun));
            for (var i = 1; i <= expectedNumberOfDaysToClearInRun; i++)
            {
                var expectedDate = DateTime.Today.AddDays(-(numberOfDaysAgoOfLastCleared - i));
                _stateRepositoryMock.Verify(r => r.SetLastStagingDateClearedAsync(expectedDate, _cancellationToken),
                    Times.Once, $"Did not set date for date {i} days ago");
            }
        }

        [TestCase(15, 1)]
        [TestCase(16, 2)]
        [TestCase(17, 3)]
        [TestCase(18, 4)]
        public async Task ThenItShouldClearStagingDataForEachDayBetweenLastClearedAndRetentionDate(
            int numberOfDaysAgoOfLastCleared,
            int expectedNumberOfDaysToClearInRun)
        {
            _stateRepositoryMock.Setup(r => r.GetLastStagingDateClearedAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTime.Today.AddDays(-numberOfDaysAgoOfLastCleared));

            await _manager.TidyCacheAsync(_cancellationToken);

            _providerRepositoryMock.Verify(r => r.ClearStagingDataForDateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
                Times.Exactly(expectedNumberOfDaysToClearInRun));
            for (var i = 1; i <= expectedNumberOfDaysToClearInRun; i++)
            {
                var expectedDate = DateTime.Today.AddDays(-(numberOfDaysAgoOfLastCleared - i));
                _providerRepositoryMock.Verify(r => r.ClearStagingDataForDateAsync(expectedDate, _cancellationToken),
                    Times.Once, $"Did not set date for date {i} days ago");
            }
        }
    }
}