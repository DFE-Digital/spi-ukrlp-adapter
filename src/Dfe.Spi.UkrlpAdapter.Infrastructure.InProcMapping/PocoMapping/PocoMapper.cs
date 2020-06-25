using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.Translation;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping
{
    public class PocoMapper : IMapper
    {
        private readonly ITranslator _translator;

        public PocoMapper(ITranslator translator)
        {
            _translator = translator;
        }

        public async Task<TDestination> MapAsync<TDestination>(object source, CancellationToken cancellationToken)
            where TDestination : class, new()
        {
            var sourceType = source.GetType();
            var mapper = GetMapperForType(sourceType);
            if (mapper == null)
            {
                throw new ArgumentException($"No mapper defined for {sourceType.FullName}", nameof(source));
            }

            return await mapper.MapAsync<TDestination>(source, cancellationToken);
        }

        private ObjectMapper GetMapperForType(Type type)
        {
            if (type == typeof(Provider) || type == typeof(PointInTimeProvider))
            {
                return new ProviderMapper(_translator);
            }

            return null;
        }
    }
}