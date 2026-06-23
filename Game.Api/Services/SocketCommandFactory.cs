using Game.Api.Sockets.Commands;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Game.Api.Services
{
    public class SocketCommandFactory
    {
        private static readonly ConcurrentDictionary<string, Func<IServiceProvider, AbstractSocketCommand>> _socketCommandGenerators = [];
        private static readonly ConcurrentDictionary<string, byte> _serverInitiatedCommandNames = [];

        /// <summary>
        /// Whether the named command is server-initiated only (<see cref="IServerInitiatedCommand"/>) and
        /// must therefore be rejected on the inbound client path. An O(1) lookup against the set built once
        /// at registration, so the inbound path pays no per-message reflection.
        /// </summary>
        public bool IsServerInitiatedOnly(string commandName)
        {
            return _serverInitiatedCommandNames.ContainsKey(commandName);
        }

        /// <summary>
        /// Whether a generator is registered for the named command. Lets the inbound client path reject an
        /// unknown command name (e.g. a stale name across deploys) with a structured rejection rather than
        /// letting it reach <see cref="CreateCommand"/>, whose throw would be misclassified as an internal
        /// fault — logged at error and surfaced to the client as an "Internal Server Error". Virtual so a test
        /// double can control known-ness without populating the static registry.
        /// </summary>
        public virtual bool IsKnownCommand(string commandName)
        {
            return _socketCommandGenerators.ContainsKey(commandName);
        }

        /// <summary>
        /// Creates the requested socket command inside a fresh DI scope.
        /// The caller is responsible for disposing the returned <see cref="IServiceScope"/>
        /// after the command has executed (and after any post-execution work such as UoW commit).
        /// </summary>
        public virtual AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo, IServiceScope scope)
        {
            if (_socketCommandGenerators.TryGetValue(commandInfo.Name, out var generator))
            {
                var command = generator(scope.ServiceProvider);
                command.SetParameters(commandInfo.Parameters);
                command.Id = commandInfo.Id;
                return command;
            }
            else
            {
                throw new InvalidOperationException($"No socket command generator found for: {commandInfo.Name}.");
            }
        }

        public static void RegisterSocketCommandGenerators()
        {
            var assembly = typeof(SocketCommandFactory).Assembly;
            var types = assembly.GetTypes();
            var serviceExtensions = typeof(ServiceProviderServiceExtensions);
            foreach (var type in types.Where(t => t.IsAssignableTo(typeof(AbstractSocketCommand)) && !t.IsAbstract))
            {
                // Single() fails fast at registration if a command ever gains a second public constructor,
                // rather than First() silently binding an arbitrary one that fails at invoke time.
                var constructor = type.GetConstructors().Single();
                var serviceDependencies = constructor.GetParameters();
                var serviceProvider = Expression.Parameter(typeof(IServiceProvider));
                var serviceInjectors = serviceDependencies.Select(p => Expression.Call(serviceExtensions, "GetRequiredService", [p.ParameterType], serviceProvider));
                var commandGenerator = Expression.Lambda<Func<IServiceProvider, AbstractSocketCommand>>(Expression.New(constructor, serviceInjectors), serviceProvider);
                var compiledGenerator = commandGenerator.Compile();

                _socketCommandGenerators.TryAdd(type.Name, compiledGenerator);

                if (type.IsAssignableTo(typeof(IServerInitiatedCommand)))
                {
                    _serverInitiatedCommandNames.TryAdd(type.Name, 0);
                }
            }
        }
    }
}
