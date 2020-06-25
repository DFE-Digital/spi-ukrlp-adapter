using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Models.Entities;

namespace Dfe.Spi.UkrlpAdapter.Domain.Events
{
    public interface IEventPublisher
    {
        Task PublishLearningProviderCreatedAsync(LearningProvider learningProvider, DateTime pointInTime, CancellationToken cancellationToken);
        Task PublishLearningProviderUpdatedAsync(LearningProvider learningProvider, DateTime pointInTime, CancellationToken cancellationToken);
    }
}