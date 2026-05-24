using Game.Application;
using Game.Core.Events;

namespace Game.Application.Tests.Fakes
{
    /// <summary>
    /// Captures dispatched domain events for assertion in unit tests.
    /// </summary>
    internal class FakeDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> DispatchedEvents { get; } = [];

        public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            DispatchedEvents.AddRange(events);
            return Task.CompletedTask;
        }
    }
}
