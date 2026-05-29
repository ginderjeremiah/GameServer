using System.Collections.Concurrent;

namespace Game.Core.Events
{
    internal class DomainEventTypeCache
    {
        private readonly ConcurrentDictionary<Type, Type[]> _cache = new();

        public Type[] GetDomainEventTypes(Type eventType)
        {
            return _cache.GetOrAdd(eventType, t =>
            {
                var types = new List<Type> { t };
                var interfaces = t.GetInterfaces();
                foreach (var implementedInterface in interfaces)
                {
                    if (typeof(IDomainEvent).IsAssignableFrom(implementedInterface))
                    {
                        types.Add(implementedInterface);
                    }
                }

                var baseType = t.BaseType;
                while (baseType != null && typeof(IDomainEvent).IsAssignableFrom(baseType))
                {

                    types.Add(baseType);
                    baseType = baseType.BaseType;
                }

                return types.ToArray();
            });
        }
    }
}
