using Game.Core;
using Game.Core.Events;
using Xunit;

namespace Game.Core.Tests.Events
{
    /// <summary>
    /// Covers the dispatcher's drain behaviour: events a handler raises on the aggregate while reacting
    /// to another event must be dispatched in the same cycle (not left pending until the next save). This
    /// is what lets a challenge completion's reward-unlock events persist and notify immediately.
    /// </summary>
    public class DomainEventDispatcherTests
    {
        [Fact]
        public async Task DispatchAsync_DrainsEventsRaisedDuringHandling()
        {
            var log = new HandledLog();
            DomainEventDispatcher.RegisterDomainEventHandler<FirstEvent, FirstEventHandler>();
            DomainEventDispatcher.RegisterDomainEventHandler<SecondEvent, SecondEventHandler>();

            var dispatcher = new DomainEventDispatcher(new StubServiceProvider(log));
            var aggregate = new TestAggregate();
            aggregate.Raise(new FirstEvent(aggregate));

            await dispatcher.DispatchAsync(aggregate);

            // The second handler ran only because the drain re-dispatched the event the first handler
            // raised mid-cycle, and the aggregate is left with nothing pending.
            Assert.Equal([nameof(FirstEvent), nameof(SecondEvent)], log.Handled);
            Assert.Empty(aggregate.DomainEvents);
        }

        // Test-only aggregate that exposes RaiseEvent so a handler can enqueue a follow-up event on it.
        private sealed class TestAggregate : AggregateRoot
        {
            public void Raise(IDomainEvent domainEvent) => RaiseEvent(domainEvent);
        }

        private sealed record FirstEvent(TestAggregate Aggregate) : IDomainEvent;

        private sealed record SecondEvent : IDomainEvent;

        // Records the order events were handled so the test can assert the cascade ran in one dispatch.
        private sealed class HandledLog
        {
            public List<string> Handled { get; } = [];
        }

        private sealed class FirstEventHandler(HandledLog log) : IDomainEventHandler<FirstEvent>
        {
            public Task HandleAsync(FirstEvent domainEvent, CancellationToken cancellationToken = default)
            {
                log.Handled.Add(nameof(FirstEvent));
                // Reacting to the first event raises a second on the same aggregate — the case the drain
                // loop must dispatch in the same cycle.
                domainEvent.Aggregate.Raise(new SecondEvent());
                return Task.CompletedTask;
            }
        }

        private sealed class SecondEventHandler(HandledLog log) : IDomainEventHandler<SecondEvent>
        {
            public Task HandleAsync(SecondEvent domainEvent, CancellationToken cancellationToken = default)
            {
                log.Handled.Add(nameof(SecondEvent));
                return Task.CompletedTask;
            }
        }

        // Minimal provider resolving only the HandledLog the test handlers depend on, so the test needs no
        // DI container package.
        private sealed class StubServiceProvider(HandledLog log) : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(HandledLog) ? log : null;
        }
    }
}
