using System;
using System.Reflection.Metadata;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.NUnit3;
using Dfe.Spi.Models;
using Dfe.Spi.UkrlpAdapter.Domain.Translation;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping;
using Moq;
using NUnit.Framework;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.UnitTests.PocoMapping
{
    public class WhenMappingGenericObject
    {
        private Mock<ITranslator> _translatorMock;
        private PocoMapper _mapper;

        [SetUp]
        public void Arrange()
        {
            _translatorMock = new Mock<ITranslator>();
            
            _mapper = new PocoMapper(_translatorMock.Object);
        }
        
        [Test, AutoData]
        public async Task ThenItShouldReturnLearningProviderWhenMappingProviderToLearningProvider(Provider source)
        {
            var actual = await _mapper.MapAsync<LearningProvider>(source, new CancellationToken());
            
            Assert.IsInstanceOf<LearningProvider>(actual);
        }

        [Test]
        public void ThenItShouldThrowExceptionIfNoMapperDefined()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
                await _mapper.MapAsync<Provider>(new object(), new CancellationToken()));
        }
    }
}