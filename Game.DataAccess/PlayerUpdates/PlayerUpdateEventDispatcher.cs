using Game.Core;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Game.DataAccess.PlayerUpdates
{
    /// <summary>
    /// Resolves the persistence handler for a dequeued <see cref="DomainEventEnvelope"/> by its type name,
    /// deserializes the inner payload, and invokes the handler against the drain scope. Adding a persisted
    /// event is a new <see cref="IPlayerUpdateHandler{TEvent}"/> plus one registration (see
    /// <c>ServiceCollectionExtensions.AddPlayerUpdateHandlers</c>) rather than an edit to a central switch.
    /// An envelope whose type matches no registered handler surfaces an <see cref="UnknownEventTypeException"/>
    /// and a payload that cannot deserialize surfaces a <see cref="JsonException"/>, so the synchronizer
    /// dead-letters either without retrying.
    /// </summary>
    internal sealed class PlayerUpdateEventDispatcher
    {
        // Type name (DomainEventEnvelope.Type) -> deserialize payload + resolve handler + invoke. Registration
        // is process-wide and idempotent (the same key is re-set to an equivalent delegate), mirroring the
        // in-process DomainEventDispatcher's static registry; reads on the drain path are lock-free.
        private static readonly ConcurrentDictionary<string, Func<IServiceProvider, string, Task>> _handlers = new();

        private readonly IServiceProvider _scopedProvider;

        public PlayerUpdateEventDispatcher(IServiceProvider scopedProvider)
        {
            _scopedProvider = scopedProvider;
        }

        /// <summary>
        /// Registers the dispatch mapping for <typeparamref name="TEvent"/>, keyed by its type name (the value
        /// <see cref="DomainEventEnvelope.Type"/> carries). The handler itself is resolved from the scope at
        /// dispatch time, so a missing or broken handler registration surfaces as an ordinary resolution
        /// failure (retried like any transient fault) rather than at registration.
        /// </summary>
        public static void Register<TEvent>()
        {
            _handlers[typeof(TEvent).Name] = async (provider, payload) =>
            {
                var updateEvent = Deserialize<TEvent>(payload);
                var handler = provider.GetRequiredService<IPlayerUpdateHandler<TEvent>>();
                await handler.HandleAsync(updateEvent);
            };
        }

        public async Task DispatchAsync(DomainEventEnvelope envelope)
        {
            if (!_handlers.TryGetValue(envelope.Type, out var handle))
            {
                throw new UnknownEventTypeException(envelope.Type);
            }

            await handle(_scopedProvider, envelope.Payload);
        }

        private static T Deserialize<T>(string json)
            => json.Deserialize<T>() ?? throw new JsonException($"Deserialized '{typeof(T).Name}' payload was null.");
    }
}
