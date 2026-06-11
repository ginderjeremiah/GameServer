using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Game.Core.Events
{
    public static class DomainEventDispatcherExtensions
    {
        public static IServiceCollection AddDomainEventDispatcher(this IServiceCollection services)
        {
            return services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        }
    }

    public class DomainEventDispatcher : IDomainEventDispatcher
    {
        private static readonly ConcurrentDictionary<Type, ConcurrentBag<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>> _domainEventHandlers = [];
        private static readonly ConcurrentDictionary<(Type, Type), byte> _registeredHandlers = [];
        private static readonly DomainEventTypeCache _domainEventTypeCache = new();

        private readonly IServiceProvider _serviceProvider;

        public DomainEventDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Safety bound on the drain loop below. A realistic event cascade is only a couple of levels
        /// deep, so exceeding this means a handler is raising events on the aggregate unboundedly —
        /// fail loudly rather than spin forever.
        /// </summary>
        private const int MaxDispatchIterations = 100;

        public async Task DispatchAsync(AggregateRoot aggregateRoot, CancellationToken cancellationToken = default)
        {
            // A handler reacting to an event may raise further events on the same aggregate (e.g.
            // completing a challenge unlocks its reward items, each raising its own unlock event). Drain
            // until the aggregate is quiescent so those secondary events are dispatched in this same
            // cycle; otherwise they linger on the aggregate until the next save, delaying their
            // persistence and any client notification driven off them.
            var iterations = 0;
            while (aggregateRoot.DomainEvents.Count > 0)
            {
                if (++iterations > MaxDispatchIterations)
                {
                    throw new InvalidOperationException(
                        $"Domain event dispatch did not settle after {MaxDispatchIterations} iterations; a handler is raising events unboundedly.");
                }

                var events = aggregateRoot.DomainEvents.ToArray();
                aggregateRoot.ClearEvents();
                await DispatchAsync(events, cancellationToken);
            }
        }

        public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            foreach (var domainEvent in events)
            {
                var eventTypes = _domainEventTypeCache.GetDomainEventTypes(domainEvent.GetType());
                foreach (var eventType in eventTypes)
                {
                    if (_domainEventHandlers.TryGetValue(eventType, out var handlers))
                    {
                        foreach (var handler in handlers)
                        {
                            await handler(_serviceProvider, domainEvent, cancellationToken);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Registers the domain event type <typeparamref name="T1"/> to trigger the handler <typeparamref name="T2"/> when dispatched.
        /// </summary>
        /// <remarks>
        /// Handlers can only be registered once per given type. Duplicate registrations will be silently ignored.
        /// </remarks>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        public static void RegisterDomainEventHandler<T1, T2>() where T1 : IDomainEvent where T2 : IDomainEventHandler<T1>
        {
            var eventType = typeof(T1);
            var handlerType = typeof(T2);

            if (!_registeredHandlers.TryAdd((eventType, handlerType), 0))
            {
                return;
            }

            var serviceExtensions = typeof(ServiceProviderServiceExtensions);
            var handlerConstructor = handlerType.GetConstructors().First();
            var serviceDependencies = handlerConstructor.GetParameters();
            var serviceProvider = Expression.Parameter(typeof(IServiceProvider));
            var serviceInjectors = serviceDependencies.Select(p => Expression.Call(serviceExtensions, "GetRequiredService", [p.ParameterType], serviceProvider));

            var handlerGenerator = Expression.New(handlerConstructor, serviceInjectors);

            var cancellationTokenParameter = Expression.Parameter(typeof(CancellationToken));
            var domainEventParameter = Expression.Parameter(typeof(IDomainEvent));
            var convertedDomainEvent = Expression.Convert(domainEventParameter, eventType);

            var handlerExecutor = Expression.Call(handlerGenerator, "HandleAsync", null, convertedDomainEvent, cancellationTokenParameter);

            var handlerAction = Expression.Lambda<Func<IServiceProvider, IDomainEvent, CancellationToken, Task>>(
                handlerExecutor, serviceProvider, domainEventParameter, cancellationTokenParameter
            ).Compile();

            _domainEventHandlers.AddOrUpdate(eventType, [handlerAction], (key, existingHandlers) =>
            {
                existingHandlers.Add(handlerAction);
                return existingHandlers;
            });
        }
    }
}
