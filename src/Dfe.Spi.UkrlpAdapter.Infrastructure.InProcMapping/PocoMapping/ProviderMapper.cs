using System;
using Dfe.Spi.Models;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping
{
    internal class ProviderMapper : ObjectMapper
    {
        protected override TDestination Map<TDestination>(object source)
        {
            var provider = source as Provider;
            if (provider == null)
            {
                throw new ArgumentException(
                    $"source must be a Provider, but received {source.GetType().FullName}", nameof(source));
            }

            if (typeof(TDestination) != typeof(LearningProvider))
            {
                throw new ArgumentException(
                    $"TDestination must be LearningProvider, but received {typeof(TDestination).FullName}", nameof(source));
            }

            var learningProvider = new LearningProvider
            {
                Name = provider.ProviderName,
            };
            return learningProvider as TDestination;
        }
    }
}