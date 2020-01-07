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
    public class WhenMappingGenericObject
    {
        [Test, AutoData]
        public async Task ThenItShouldReturnLearningProviderWhenMappingProviderToLearningProvider(Provider source)
        {
            var mapper = new PocoMapper();

            var actual = await mapper.MapAsync<LearningProvider>(source, new CancellationToken());
            
            Assert.IsInstanceOf<LearningProvider>(actual);
        }

        [Test]
        public void ThenItShouldThrowExceptionIfNoMapperDefined()
        {
            var mapper = new PocoMapper();

            Assert.ThrowsAsync<ArgumentException>(async () =>
                await mapper.MapAsync<Provider>(new object(), new CancellationToken()));
        }
    }
}