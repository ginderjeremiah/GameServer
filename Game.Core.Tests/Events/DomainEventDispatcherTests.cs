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

            await dispatcher.DispatchAsync(aggregate, TestContext.Current.CancellationToken);

            // The second handler ran only because the drain re-dispatched the event the first handler
            // raised mid-cycle, and the aggregate is left with nothing pending.
            Assert.Equal([nameof(FirstEvent), nameof(SecondEvent)], log.Handled);
            Assert.Empty(aggregate.DomainEvents);
        }

        [Fact]
        public async Task DispatchAsync_HandlerRaisesEventsUnboundedly_ThrowsAfterSafetyBound()
        {
            DomainEventDispatcher.RegisterDomainEventHandler<LoopEvent, LoopEventHandler>();

            var dispatcher = new DomainEventDispatcher(new StubServiceProvider(new HandledLog()));
            var aggregate = new TestAggregate();
            aggregate.Raise(new LoopEvent(aggregate));

            // A handler that keeps raising a fresh event on the aggregate never lets the drain settle; the
            // safety bound surfaces that as a thrown exception rather than spinning forever.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dispatcher.DispatchAsync(aggregate, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task DispatchAsync_MultipleHandlersForSameEvent_RunsAll()
        {
            var log = new HandledLog();
            DomainEventDispatcher.RegisterDomainEventHandler<MultiEvent, MultiHandlerA>();
            DomainEventDispatcher.RegisterDomainEventHandler<MultiEvent, MultiHandlerB>();

            var dispatcher = new DomainEventDispatcher(new StubServiceProvider(log));

            await dispatcher.DispatchAsync(new IDomainEvent[] { new MultiEvent() }, TestContext.Current.CancellationToken);

            // Both handlers registered against the same event type fire — exercising the registry's
            // add-to-existing path and the per-event handler fan-out. The handler collection is an
            // unordered ConcurrentBag, so assert membership rather than order.
            Assert.Equal(2, log.Handled.Count);
            Assert.Contains("A", log.Handled);
            Assert.Contains("B", log.Handled);
        }

        // Test-only aggregate that exposes RaiseEvent so a handler can enqueue a follow-up event on it.
        private sealed class TestAggregate : AggregateRoot
        {
            public void Raise(IDomainEvent domainEvent) => RaiseEvent(domainEvent);
        }

        private sealed record FirstEvent(TestAggregate Aggregate) : IDomainEvent;

        private sealed record SecondEvent : IDomainEvent;

        private sealed record LoopEvent(TestAggregate Aggregate) : IDomainEvent;

        private sealed record MultiEvent : IDomainEvent;

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

        private sealed class MultiHandlerA(HandledLog log) : IDomainEventHandler<MultiEvent>
        {
            public Task HandleAsync(MultiEvent domainEvent, CancellationToken cancellationToken = default)
            {
                log.Handled.Add("A");
                return Task.CompletedTask;
            }
        }

        private sealed class MultiHandlerB(HandledLog log) : IDomainEventHandler<MultiEvent>
        {
            public Task HandleAsync(MultiEvent domainEvent, CancellationToken cancellationToken = default)
            {
                log.Handled.Add("B");
                return Task.CompletedTask;
            }
        }

        // Always re-raises a fresh event on the aggregate, so the drain loop can never settle.
        private sealed class LoopEventHandler : IDomainEventHandler<LoopEvent>
        {
            public Task HandleAsync(LoopEvent domainEvent, CancellationToken cancellationToken = default)
            {
                domainEvent.Aggregate.Raise(new LoopEvent(domainEvent.Aggregate));
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
