using Game.Core;
using Game.Core.Events;
using Xunit;

namespace Game.Core.Tests.Events
{
    /// <summary>
    /// Covers the dispatcher's drain behaviour: events a handler raises on the aggregate while reacting
    /// to another event must be dispatched in the same cycle (not left pending until the next save). This
    /// is what lets a challenge completion's reward-unlock events persist and notify immediately. Also
    /// covers per-handler failure isolation: one handler throwing must not silently drop the remaining
    /// events/handlers in the batch (or the drain), and the collected failures are surfaced afterwards.
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
        public void RegisterDomainEventHandler_HandlerWithMultipleConstructors_ThrowsAtRegistration()
        {
            // Registration binds the handler via GetConstructors().Single(), so an ambiguous handler
            // (more than one public constructor) fails fast here rather than silently binding an
            // arbitrary constructor that would only fail when the event is later dispatched.
            Assert.Throws<InvalidOperationException>(
                DomainEventDispatcher.RegisterDomainEventHandler<AmbiguousEvent, AmbiguousCtorHandler>);
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
            // add-to-existing path and the per-event handler fan-out. Handlers run in registration order
            // (A registered before B), the guarantee the ordered handler collection provides.
            Assert.Equal(["A", "B"], log.Handled);
        }

        [Fact]
        public async Task DispatchAsync_MultipleHandlersForSameEvent_RunInRegistrationOrder()
        {
            var log = new HandledLog();
            // Register in B-then-A order; dispatch must honour that order rather than any incidental
            // collection ordering — the deterministic-order guarantee this issue is about.
            DomainEventDispatcher.RegisterDomainEventHandler<OrderedEvent, OrderedHandlerB>();
            DomainEventDispatcher.RegisterDomainEventHandler<OrderedEvent, OrderedHandlerA>();

            var dispatcher = new DomainEventDispatcher(new StubServiceProvider(log));

            await dispatcher.DispatchAsync(new IDomainEvent[] { new OrderedEvent() }, TestContext.Current.CancellationToken);

            Assert.Equal(["B", "A"], log.Handled);
        }

        [Fact]
        public async Task DispatchAsync_OneHandlerThrows_StillRunsOtherHandlersAndSurfacesFailure()
        {
            var log = new HandledLog();
            DomainEventDispatcher.RegisterDomainEventHandler<SharedEvent, ThrowingHandler>();
            DomainEventDispatcher.RegisterDomainEventHandler<SharedEvent, RecordingHandler>();

            var dispatcher = new DomainEventDispatcher(new StubServiceProvider(log));

            // A handler throwing must not stop the other handler registered for the same event from
            // running, and the failure is surfaced rather than swallowed.
            var ex = await Assert.ThrowsAsync<AggregateException>(
                () => dispatcher.DispatchAsync(new IDomainEvent[] { new SharedEvent() }, TestContext.Current.CancellationToken));

            Assert.Single(ex.InnerExceptions);
            Assert.Contains("Recording", log.Handled);
        }

        [Fact]
        public async Task DispatchAsync_HandlerThrowsMidDrain_StillDispatchesRemainingEventsAndDrains()
        {
            var log = new HandledLog();
            DomainEventDispatcher.RegisterDomainEventHandler<ThrowEvent, ThrowingHandler>();
            DomainEventDispatcher.RegisterDomainEventHandler<SiblingEvent, SiblingEventHandler>();
            DomainEventDispatcher.RegisterDomainEventHandler<ChildEvent, ChildEventHandler>();

            var dispatcher = new DomainEventDispatcher(new StubServiceProvider(log));
            var aggregate = new TestAggregate();
            aggregate.Raise(new ThrowEvent());
            aggregate.Raise(new SiblingEvent(aggregate));

            // The first event's handler throws, but its sibling in the same batch still dispatches, and the
            // child event that sibling raises is still drained in the same cycle — the regression this issue
            // is about (a mid-batch throw must not silently drop the already-cleared remaining events). The
            // failure is still surfaced afterwards.
            var ex = await Assert.ThrowsAsync<AggregateException>(
                () => dispatcher.DispatchAsync(aggregate, TestContext.Current.CancellationToken));

            Assert.Single(ex.InnerExceptions);
            Assert.Contains("Sibling", log.Handled);
            Assert.Contains("Child", log.Handled);
            Assert.Empty(aggregate.DomainEvents);
        }

        [Fact]
        public async Task DispatchAsync_MultipleHandlersThrow_AggregatesEveryFailure()
        {
            var log = new HandledLog();
            DomainEventDispatcher.RegisterDomainEventHandler<MultiThrowEvent, ThrowingHandler>();
            DomainEventDispatcher.RegisterDomainEventHandler<MultiThrowEvent, AlsoThrowingHandler>();

            var dispatcher = new DomainEventDispatcher(new StubServiceProvider(log));

            // Every failing handler's exception is collected, not just the first one to throw.
            var ex = await Assert.ThrowsAsync<AggregateException>(
                () => dispatcher.DispatchAsync(new IDomainEvent[] { new MultiThrowEvent() }, TestContext.Current.CancellationToken));

            Assert.Equal(2, ex.InnerExceptions.Count);
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

        private sealed record OrderedEvent : IDomainEvent;

        private sealed record SharedEvent : IDomainEvent;

        private sealed record ThrowEvent : IDomainEvent;

        private sealed record SiblingEvent(TestAggregate Aggregate) : IDomainEvent;

        private sealed record ChildEvent : IDomainEvent;

        private sealed record MultiThrowEvent : IDomainEvent;

        private sealed record AmbiguousEvent : IDomainEvent;

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

        private sealed class OrderedHandlerA(HandledLog log) : IDomainEventHandler<OrderedEvent>
        {
            public Task HandleAsync(OrderedEvent domainEvent, CancellationToken cancellationToken = default)
            {
                log.Handled.Add("A");
                return Task.CompletedTask;
            }
        }

        private sealed class OrderedHandlerB(HandledLog log) : IDomainEventHandler<OrderedEvent>
        {
            public Task HandleAsync(OrderedEvent domainEvent, CancellationToken cancellationToken = default)
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

        // Throws for every event it handles, exercising per-handler isolation: its failure must not abort
        // the rest of the batch or the drain.
        private sealed class ThrowingHandler :
            IDomainEventHandler<SharedEvent>,
            IDomainEventHandler<ThrowEvent>,
            IDomainEventHandler<MultiThrowEvent>
        {
            public Task HandleAsync(SharedEvent domainEvent, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("handler failed");
            public Task HandleAsync(ThrowEvent domainEvent, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("handler failed");
            public Task HandleAsync(MultiThrowEvent domainEvent, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("handler failed");
        }

        // A second failing handler so a multi-failure batch can assert every exception is collected.
        private sealed class AlsoThrowingHandler : IDomainEventHandler<MultiThrowEvent>
        {
            public Task HandleAsync(MultiThrowEvent domainEvent, CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("other handler failed");
        }

        // Records that it ran, proving a sibling handler's throw did not skip it.
        private sealed class RecordingHandler(HandledLog log) : IDomainEventHandler<SharedEvent>
        {
            public Task HandleAsync(SharedEvent domainEvent, CancellationToken cancellationToken = default)
            {
                log.Handled.Add("Recording");
                return Task.CompletedTask;
            }
        }

        private sealed class SiblingEventHandler(HandledLog log) : IDomainEventHandler<SiblingEvent>
        {
            public Task HandleAsync(SiblingEvent domainEvent, CancellationToken cancellationToken = default)
            {
                log.Handled.Add("Sibling");
                // Raises a follow-up the drain must still dispatch even though a sibling event's handler threw.
                domainEvent.Aggregate.Raise(new ChildEvent());
                return Task.CompletedTask;
            }
        }

        private sealed class ChildEventHandler(HandledLog log) : IDomainEventHandler<ChildEvent>
        {
            public Task HandleAsync(ChildEvent domainEvent, CancellationToken cancellationToken = default)
            {
                log.Handled.Add("Child");
                return Task.CompletedTask;
            }
        }

        // Two public constructors, so registration's GetConstructors().Single() throws — the ambiguity
        // the fail-fast guard is meant to catch.
        private sealed class AmbiguousCtorHandler : IDomainEventHandler<AmbiguousEvent>
        {
            public AmbiguousCtorHandler() { }
            public AmbiguousCtorHandler(HandledLog log) { }
            public Task HandleAsync(AmbiguousEvent domainEvent, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
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
