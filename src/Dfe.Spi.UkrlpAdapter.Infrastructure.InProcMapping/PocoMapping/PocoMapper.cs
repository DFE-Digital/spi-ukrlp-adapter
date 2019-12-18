using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping
{
    public class PocoMapper : IMapper
    {
        private static readonly Dictionary<Type, ObjectMapper> Mappers = new Dictionary<Type, ObjectMapper>
        {
            {typeof(Provider), new ProviderMapper()},
        };

        public async Task<TDestination> MapAsync<TDestination>(object source, CancellationToken cancellationToken)
            where TDestination : class, new()
        {
            var sourceType = source.GetType();
            if (!Mappers.ContainsKey(sourceType))
            {
                throw new ArgumentException($"No mapper defined for {sourceType.FullName}", nameof(source));
            }

            var mapper = Mappers[sourceType];
            return await mapper.MapAsync<TDestination>(source, cancellationToken);
        }
    }
}