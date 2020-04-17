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

        [Test, AutoData]
        public async Task ThenItShouldMapProvidersToLearningProviders(Provider[] providers)
        {
            var ukprns = _fixture.Create<long[]>();
            var ids = ukprns.Select(x => x.ToString()).ToArray();

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

        [Test, NonRecursiveAutoData]
        public async Task ThenItShouldReturnMappedLearningProviders(LearningProvider[] learningProviders)
        {
            foreach (var learningProvider in learningProviders)
            {
                learningProvider.Ukprn = _fixture.Create<long>();
            }
            var ukprns = learningProviders.Select(x => x.Ukprn.Value).ToArray();
            var ids = ukprns.Select(x => x.ToString()).ToArray();
            var providers = new Provider[learningProviders.Length];
            for (var i = 0; i < learningProviders.Length; i++)
            {
                providers[i] = new Provider {UnitedKingdomProviderReferenceNumber = learningProviders[i].Ukprn.Value};
            }

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
    }
}