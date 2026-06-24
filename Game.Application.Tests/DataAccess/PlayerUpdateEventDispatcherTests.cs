using Game.Core;
using Game.DataAccess;
using Game.DataAccess.PlayerUpdates;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using Xunit;

namespace Game.Application.Tests.DataAccess
{
    /// <summary>
    /// Unit tests for <see cref="PlayerUpdateEventDispatcher"/> in isolation (no database): a registered type
    /// is deserialized and routed to its handler, an unregistered type surfaces an
    /// <see cref="UnknownEventTypeException"/>, and an unparseable payload surfaces a <see cref="JsonException"/>
    /// — the two poison-message signals <see cref="DataProviderSynchronizer"/> dead-letters without retrying.
    /// The full per-event persistence behaviour is covered end-to-end by <see cref="DataProviderSynchronizerTests"/>.
    /// </summary>
    public class PlayerUpdateEventDispatcherTests
    {
        private sealed record FakeUpdateEvent(int Value);

        private sealed class RecordingHandler : IPlayerUpdateHandler<FakeUpdateEvent>
        {
            public FakeUpdateEvent? Received { get; private set; }

            public Task HandleAsync(FakeUpdateEvent updateEvent)
            {
                Received = updateEvent;
                return Task.CompletedTask;
            }
        }

        private static IServiceProvider ProviderWith(RecordingHandler handler)
            => new ServiceCollection()
                .AddScoped<IPlayerUpdateHandler<FakeUpdateEvent>>(_ => handler)
                .BuildServiceProvider();

        [Fact]
        public async Task DispatchAsync_RegisteredType_DeserializesPayloadAndInvokesHandler()
        {
            PlayerUpdateEventDispatcher.Register<FakeUpdateEvent>();
            var handler = new RecordingHandler();
            var dispatcher = new PlayerUpdateEventDispatcher(ProviderWith(handler));

            var envelope = new DomainEventEnvelope
            {
                Type = nameof(FakeUpdateEvent),
                Payload = new FakeUpdateEvent(42).Serialize(),
            };

            await dispatcher.DispatchAsync(envelope);

            Assert.Equal(42, handler.Received?.Value);
        }

        [Fact]
        public async Task DispatchAsync_UnregisteredType_ThrowsUnknownEventType()
        {
            var dispatcher = new PlayerUpdateEventDispatcher(new ServiceCollection().BuildServiceProvider());
            var envelope = new DomainEventEnvelope { Type = "NotARegisteredEvent", Payload = "{}" };

            await Assert.ThrowsAsync<UnknownEventTypeException>(() => dispatcher.DispatchAsync(envelope));
        }

        [Fact]
        public async Task DispatchAsync_NullPayload_ThrowsJsonException()
        {
            PlayerUpdateEventDispatcher.Register<FakeUpdateEvent>();
            var dispatcher = new PlayerUpdateEventDispatcher(ProviderWith(new RecordingHandler()));

            // A JSON "null" literal deserializes to a null event; the dispatcher rejects it as a poison payload.
            var envelope = new DomainEventEnvelope { Type = nameof(FakeUpdateEvent), Payload = "null" };

            await Assert.ThrowsAsync<JsonException>(() => dispatcher.DispatchAsync(envelope));
        }

        // The dispatch key is the unqualified type name the persisted envelope carries (see Register), so two
        // event types with the same simple name across namespaces would collide. Registration fails loud rather
        // than silently overwriting and misrouting one type's writes to the other's handler.
        private static class NamespaceA { public sealed record Collide(int Value); }
        private static class NamespaceB { public sealed record Collide(int Value); }

        [Fact]
        public void Register_DifferentTypeWithCollidingSimpleName_Throws()
        {
            PlayerUpdateEventDispatcher.Register<NamespaceA.Collide>();

            Assert.Throws<InvalidOperationException>(PlayerUpdateEventDispatcher.Register<NamespaceB.Collide>);
        }

        [Fact]
        public void Register_SameTypeTwice_IsIdempotent()
        {
            PlayerUpdateEventDispatcher.Register<FakeUpdateEvent>();

            // Re-registering the identical type is the normal startup case (the test suite itself does it across
            // methods) and must not throw the collision guard.
            PlayerUpdateEventDispatcher.Register<FakeUpdateEvent>();
        }
    }
}
