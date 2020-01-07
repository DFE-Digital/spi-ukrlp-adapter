using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping
{
    internal abstract class ObjectMapper
    {
        internal virtual Task<TDestination> MapAsync<TDestination>(object source, CancellationToken cancellationToken)
            where TDestination : class, new()
        {
            try
            {
                var mapped = Map<TDestination>(source);
                return Task.FromResult(mapped);
            }
            catch (Exception ex)
            {
                return Task.FromException<TDestination>(ex);
            }
        }

        protected virtual TDestination Map<TDestination>(object source)
            where TDestination : class, new()
        {
            throw new NotImplementedException("Must either override Map or MapAsync");
        }
    }
}