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
        public async Task ThenItShouldGetProvidersFromApi()
        {
            var ukprns = _fixture.Create<long[]>();
            
            await _manager.GetLearningProvidersAsync(ukprns.Select(x => x.ToString()).ToArray(), null, _cancellationToken);

            _ukrlpApiClientMock.Verify(c => c.GetProvidersAsync(
                    It.Is<long[]>(x => x.Length == ukprns.Length), _cancellationToken),
                Times.Once);
        }

        [Test]
        public void ThenItShouldThrowExceptionIfAnIdIsNotNumeric()
        {
            var ids = new[] {"12345678", "NotANumber", "98765432"};
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProvidersAsync(ids, null, _cancellationToken));
        }

        [Test]
        public void ThenItShouldThrowExceptionIfIdIsNot8Digits()
        {
            var ids = new[] {"12345678", "123456789", "98765432"};
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _manager.GetLearningProvidersAsync(ids, null, _cancellationToken));
        }

        [Test]
        public async Task ThenItShouldMapProvidersToLearningProviders()
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

            await _manager.GetLearningProvidersAsync(ids, null, _cancellationToken);

            _mapperMock.Verify(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken),
                Times.Exactly(providers.Length));
            for (var i = 0; i < providers.Length; i++)
            {
                _mapperMock.Verify(m => m.MapAsync<LearningProvider>(providers[i], _cancellationToken),
                    Times.Once, $"Expected to map provider at index {i} exactly once");
            }
        }

        [Test]
        public async Task ThenItShouldReturnMappedLearningProviders()
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
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync((Provider provider, CancellationToken ct) => learningProviders.Single(x => x.Ukprn == provider.UnitedKingdomProviderReferenceNumber));

            var actual = await _manager.GetLearningProvidersAsync(ids, null, _cancellationToken);

            Assert.AreEqual(learningProviders.Length, actual.Length);
            for (var i = 0; i < learningProviders.Length; i++)
            {
                Assert.AreSame(learningProviders[i], actual[i],
                    $"Expected {i} to be same");
            }
        }

        [Test]
        public async Task ThenItShouldReturnArrayOfSameSizeWithNullsIfNotAllProvidersCanBeFound()
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
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync((Provider provider, CancellationToken ct) => learningProviders.Single(x => x.Ukprn == provider.UnitedKingdomProviderReferenceNumber));
            
            var actual = await _manager.GetLearningProvidersAsync(ids, null, _cancellationToken);

            Assert.AreEqual(ukprns.Length, actual.Length);
            Assert.IsNotNull(actual[0]);
            Assert.IsNull(actual[1]);
            Assert.IsNotNull(actual[2]);
        }

        [Test]
        public async Task ThenItShouldReturnResultsInSameOrderAsRequest()
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
            _mapperMock.Setup(m => m.MapAsync<LearningProvider>(It.IsAny<Provider>(), _cancellationToken))
                .ReturnsAsync((Provider provider, CancellationToken ct) => learningProviders.Single(x => x.Ukprn == provider.UnitedKingdomProviderReferenceNumber));
         
            var actual = await _manager.GetLearningProvidersAsync(ids, null, _cancellationToken);

            Assert.AreEqual(ukprns.Length, actual.Length);
            Assert.AreEqual(ukprns[0], actual[0].Ukprn);
            Assert.AreEqual(ukprns[1], actual[1].Ukprn);
            Assert.AreEqual(ukprns[2], actual[2].Ukprn);
        }
    }
}