using System.Threading;
using System.Threading.Tasks;

namespace Dfe.Spi.UkrlpAdapter.Domain.Mapping
{
    public interface IMapper
    {
        Task<TDestination> MapAsync<TDestination>(object source, CancellationToken cancellationToken)
            where TDestination : class, new();
    }
}