using System;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
using Dfe.Spi.Models;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping;
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.UnitTests.PocoMapping
{
    public class WhenMappingProviderToLearningProvider
    {
        [Test, AutoData]
        public async Task ThenItShouldReturnLearningProvider(Provider source)
        {
            var mapper = new ProviderMapper();

            var actual = await mapper.MapAsync<LearningProvider>(source, new CancellationToken());

            Assert.IsNotNull(actual);
            Assert.IsInstanceOf<LearningProvider>(actual);
        }

        [Test, AutoData]
        public async Task ThenItShouldMapEstablishmentToLearningProvider(Provider source)
        {
            var mapper = new ProviderMapper();

            var actual = await mapper.MapAsync<LearningProvider>(source, new CancellationToken()) as LearningProvider;

            Assert.IsNotNull(actual);
            Assert.AreEqual(source.ProviderName, actual.Name);
        }

        [Test]
        public void ThenItShouldThrowExceptionIfSourceIsNotEstablishment()
        {
            var mapper = new ProviderMapper();

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await mapper.MapAsync<LearningProvider>(new object(), new CancellationToken()));
        }

        [Test]
        public void ThenItShouldThrowExceptionIfDestinationIsNotLearningProvider()
        {
            var mapper = new ProviderMapper();

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await mapper.MapAsync<object>(new Provider(), new CancellationToken()));
        }
    }
}