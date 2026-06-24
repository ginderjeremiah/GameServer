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
        // Type name (DomainEventEnvelope.Type) -> the registering event type plus its deserialize-resolve-invoke
        // delegate. The key is the *unqualified* type name (the value the envelope carries), so two persisted
        // event types sharing a simple name across namespaces would collide on the same key; the registration
        // guards against that by throwing rather than silently overwriting (see Register). Registration is
        // process-wide and idempotent for the same type, mirroring the in-process DomainEventDispatcher's static
        // registry; reads on the drain path are lock-free.
        private static readonly ConcurrentDictionary<string, Registration> _handlers = new();

        private sealed record Registration(Type EventType, Func<IServiceProvider, string, Task> Handle);

        private readonly IServiceProvider _scopedProvider;

        public PlayerUpdateEventDispatcher(IServiceProvider scopedProvider)
        {
            _scopedProvider = scopedProvider;
        }

        /// <summary>
        /// Registers the dispatch mapping for <typeparamref name="TEvent"/>, keyed by its type name (the value
        /// <see cref="DomainEventEnvelope.Type"/> carries). The handler itself is resolved from the scope at
        /// dispatch time, so a missing or broken handler registration surfaces as an ordinary resolution
        /// failure (retried like any transient fault) rather than at registration. Re-registering the same type
        /// is idempotent, but registering a *different* type whose unqualified name collides with one already
        /// registered throws: the key is the simple name the wire envelope carries, so a silent overwrite would
        /// route one type's persisted writes to the other's handler with no error. Failing loud at startup keeps
        /// that latent collision from ever reaching the drain path.
        /// </summary>
        public static void Register<TEvent>()
        {
            var registration = new Registration(typeof(TEvent), async (provider, payload) =>
            {
                var updateEvent = Deserialize<TEvent>(payload);
                var handler = provider.GetRequiredService<IPlayerUpdateHandler<TEvent>>();
                await handler.HandleAsync(updateEvent);
            });

            _handlers.AddOrUpdate(typeof(TEvent).Name, registration, (name, existing) =>
            {
                if (existing.EventType != typeof(TEvent))
                {
                    throw new InvalidOperationException(
                        $"Two player-update event types share the unqualified name '{name}' " +
                        $"('{existing.EventType.FullName}' and '{typeof(TEvent).FullName}'). The dispatch key is " +
                        "the simple name the persisted envelope carries, so they would overwrite each other and " +
                        "misroute writes. Rename one event type (or switch the key to the assembly-qualified name).");
                }

                return registration;
            });
        }

        public async Task DispatchAsync(DomainEventEnvelope envelope)
        {
            if (!_handlers.TryGetValue(envelope.Type, out var registration))
            {
                throw new UnknownEventTypeException(envelope.Type);
            }

            await registration.Handle(_scopedProvider, envelope.Payload);
        }

        private static T Deserialize<T>(string json)
            => json.Deserialize<T>() ?? throw new JsonException($"Deserialized '{typeof(T).Name}' payload was null.");
    }
}
