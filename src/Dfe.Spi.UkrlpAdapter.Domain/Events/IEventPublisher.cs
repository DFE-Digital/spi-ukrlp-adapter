using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Models.Entities;

namespace Dfe.Spi.UkrlpAdapter.Domain.Events
{
    public interface IEventPublisher
    {
        Task PublishLearningProviderCreatedAsync(LearningProvider learningProvider, CancellationToken cancellationToken);
        Task PublishLearningProviderUpdatedAsync(LearningProvider learningProvider, CancellationToken cancellationToken);
    }
}