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
    public class WhenGettingLearningProviders
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
        public async Task ThenItShouldGetProvidersFromApiIfReadFromLive()
        {
            var ukprns = _fixture.Create<long[]>();

            await _manager.GetLearningProvidersAsync(ukprns.Select(x => x.ToString()).ToArray(), null, true, null, _cancellationToken);

            _ukrlpApiClientMock.Verify(c => c.GetProvidersAsync(
                    It.Is<long[]>(x => x.Length == ukprns.Length), _cancellationToken),
                Times.Once);
            _providerRepository.Verify(c => c.GetProvidersAsync(It.IsAny<long[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Test]
        public async Task ThenItShouldGetProvidersFromCacheIfNotReadFromLive()
        {
            var ukprns = _fixture.Create<long[]>();

            await _manager.GetLearningProvidersAsync(ukprns.Select(x => x.ToString()).ToArray(), null, false, null, _cancellationToken);

            _providerRepository.Verify(c => c.GetProvidersAsync(
                    It.Is<long[]>(x => x.Length == ukprns.Length), It.IsAny<DateTime?>(), _cancellationToken),
                Times.Once);
            _ukrlpApiClientMock.Verify(c => c.GetProvidersAsync(It.IsAny<long[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [TestCase(true, null)]
        [TestCase(false, null)]
        [TestCase(true, "2020-06-16")]
        [TestCase(false, "2020-06-16")]
        public void ThenItShouldThrowExceptionIfAnIdIsNotNumeric(bool readFromLive, DateTime? pointInTime)
        {
            var ids = new[] {"12345678", "NotANumber", "98765432"};
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProvidersAsync(ids, null, readFromLive, pointInTime, _cancellationToken));
        }

        [TestCase(true, null)]
        [TestCase(false, null)]
        [TestCase(true, "2020-06-16")]
        [TestCase(false, "2020-06-16")]
        public void ThenItShouldThrowExceptionIfIdIsNot8Digits(bool readFromLive, DateTime? pointInTime)
        {
            var ids = new[] {"12345678", "123456789", "98765432"};
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProvidersAsync(ids, null, readFromLive, pointInTime, _cancellationToken));
        }

        [TestCase(true, null)]
        [TestCase(false, null)]
        [TestCase(true, "2020-06-16")]
        [TestCase(false, "2020-06-16")]
        public async Task ThenItShouldMapProvidersToLearningProviders(bool readFromLive, DateTime? pointInTime)
        {
            var ukprns = _fixture.Create<long[]>();
            var providers = ukprns
                .Select(ukprn => new Provider {UnitedKingdomProviderReferenceNumber = ukprn})
                .ToArray();
            var ids = ukprns
                .Select(x => x.ToString())
                .ToArray();

            _ukrlpApiClientMock.Setup(c => c.GetProvidersAsync(It.IsAny<long[]>(), _cancellationToken))
                .ReturnsAsync(providers);
            _providerRepository.Setup(c => c.GetProvidersAsync(It.IsAny<long[]>(), It.IsAny<DateTime?>(), _cancellationToken))
                .ReturnsAsync(providers);

            await _manager.GetLearningProvidersAsync(ids, null, readFromLive, pointInTime, _cancellationToken);

            _mapperMock.Verify(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken),
                Times.Exactly(providers.Length));
            for (var i = 0; i < providers.Length; i++)
            {
                _mapperMock.Verify(m => m.MapAsync<LearningProvider>(providers[i], _cancellationToken),
                    Times.Once, $"Expected to map provider at index {i} exactly once");
            }
        }

        [TestCase(true, null)]
        [TestCase(false, null)]
        [TestCase(true, "2020-06-16")]
        [TestCase(false, "2020-06-16")]
        public async Task ThenItShouldReturnMappedLearningProviders(bool readFromLive, DateTime? pointInTime)
        {
            var ukprns = _fixture.Create<long[]>();
            var learningProviders = ukprns
                .Select(ukprn => new LearningProvider {Ukprn = ukprn})
                .ToArray();
            var providers = ukprns
                .Select(ukprn => new Provider {UnitedKingdomProviderReferenceNumber = ukprn})
                .ToArray();
            var ids = ukprns
                .Select(x => x.ToString())
                .ToArray();
            _ukrlpApiClientMock.Setup(c => c.GetProvidersAsync(It.IsAny<long[]>(), _cancellationToken))
                .ReturnsAsync(providers);
            _providerRepository.Setup(c => c.GetProvidersAsync(It.IsAny<long[]>(), It.IsAny<DateTime?>(), _cancellationToken))
                .ReturnsAsync(providers);
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync((Provider provider, CancellationToken ct) =>
                    learningProviders.Single(x => x.Ukprn == provider.UnitedKingdomProviderReferenceNumber));

            var actual = await _manager.GetLearningProvidersAsync(ids, null, readFromLive, pointInTime, _cancellationToken);

            Assert.AreEqual(learningProviders.Length, actual.Length);
            for (var i = 0; i < learningProviders.Length; i++)
            {
                Assert.AreSame(learningProviders[i], actual[i],
                    $"Expected {i} to be same");
            }
        }

        [TestCase(true, null)]
        [TestCase(false, null)]
        [TestCase(true, "2020-06-16")]
        [TestCase(false, "2020-06-16")]
        public async Task ThenItShouldReturnArrayOfSameSizeWithNullsIfNotAllProvidersCanBeFound(bool readFromLive, DateTime? pointInTime)
        {
            var ukprns = new[] {_fixture.Create<long>(), _fixture.Create<long>(), _fixture.Create<long>()};
            var providers = new[]
            {
                new Provider {UnitedKingdomProviderReferenceNumber = ukprns[0]},
                new Provider {UnitedKingdomProviderReferenceNumber = ukprns[2]},
            };
            var learningProviders = providers
                .Select(provider => new LearningProvider {Ukprn = provider.UnitedKingdomProviderReferenceNumber})
                .ToArray();
            var ids = ukprns
                .Select(x => x.ToString())
                .ToArray();
            _ukrlpApiClientMock.Setup(c => c.GetProvidersAsync(It.IsAny<long[]>(), _cancellationToken))
                .ReturnsAsync(providers);
            _providerRepository.Setup(c => c.GetProvidersAsync(It.IsAny<long[]>(), It.IsAny<DateTime?>(), _cancellationToken))
                .ReturnsAsync(providers);
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync((Provider provider, CancellationToken ct) =>
                    learningProviders.Single(x => x.Ukprn == provider.UnitedKingdomProviderReferenceNumber));

            var actual = await _manager.GetLearningProvidersAsync(ids, null, readFromLive, pointInTime, _cancellationToken);

            Assert.AreEqual(ukprns.Length, actual.Length);
            Assert.IsNotNull(actual[0]);
            Assert.IsNull(actual[1]);
            Assert.IsNotNull(actual[2]);
        }

        [TestCase(true, null)]
        [TestCase(false, null)]
        [TestCase(true, "2020-06-16")]
        [TestCase(false, "2020-06-16")]
        public async Task ThenItShouldReturnResultsInSameOrderAsRequest(bool readFromLive, DateTime? pointInTime)
        {
            var ukprns = new[] {_fixture.Create<long>(), _fixture.Create<long>(), _fixture.Create<long>()};
            var providers = new[]
            {
                new Provider {UnitedKingdomProviderReferenceNumber = ukprns[2]},
                new Provider {UnitedKingdomProviderReferenceNumber = ukprns[0]},
                new Provider {UnitedKingdomProviderReferenceNumber = ukprns[1]},
            };
            var learningProviders = providers
                .Select(provider => new LearningProvider {Ukprn = provider.UnitedKingdomProviderReferenceNumber})
                .ToArray();
            var ids = ukprns
                .Select(x => x.ToString())
                .ToArray();
            _ukrlpApiClientMock.Setup(c => c.GetProvidersAsync(It.IsAny<long[]>(), _cancellationToken))
                .ReturnsAsync(providers);
            _providerRepository.Setup(c => c.GetProvidersAsync(It.IsAny<long[]>(), It.IsAny<DateTime?>(), _cancellationToken))
                .ReturnsAsync(providers);
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync((Provider provider, CancellationToken ct) =>
                    learningProviders.Single(x => x.Ukprn == provider.UnitedKingdomProviderReferenceNumber));

            var actual = await _manager.GetLearningProvidersAsync(ids, null, readFromLive, pointInTime, _cancellationToken);

            Assert.AreEqual(ukprns.Length, actual.Length);
            Assert.AreEqual(ukprns[0], actual[0].Ukprn);
            Assert.AreEqual(ukprns[1], actual[1].Ukprn);
            Assert.AreEqual(ukprns[2], actual[2].Ukprn);
        }
    }
}