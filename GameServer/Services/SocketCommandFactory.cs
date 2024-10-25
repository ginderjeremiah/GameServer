using GameServer.Sockets.Commands;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace GameServer.Services
{
    public class SocketCommandFactory
    {
        private static readonly ConcurrentDictionary<string, Func<IServiceProvider, AbstractSocketCommand>> _socketCommandGenerators = [];
        private readonly IServiceProvider _serviceProvider;

        public SocketCommandFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            if (_socketCommandGenerators.IsEmpty)
            {
                RegisterSocketCommands();
            }
        }

        public AbstractSocketCommand CreateCommand(SocketCommandInfo commandInfo)
        {
            if (_socketCommandGenerators.TryGetValue(commandInfo.Name, out var generator))
            {
                _serviceProvider.CreateScope();
                var command = generator(_serviceProvider);
                command.SetParameters(commandInfo.Parameters);
                command.Id = commandInfo.Id;
                return command;
            }
            else
            {
                throw new InvalidOperationException($"No socket command generator found for: {commandInfo.Name}");
            }
        }

        private void RegisterSocketCommands()
        {
            var assembly = typeof(SocketCommandFactory).Assembly;
            var types = assembly.GetTypes();
            var serviceExtensions = typeof(ServiceProviderServiceExtensions);
            foreach (var type in types.Where(t => t.IsAssignableTo(typeof(AbstractSocketCommand)) && !t.IsAbstract))
            {
                var constructor = type.GetConstructors().First();
                var parameters = constructor.GetParameters();
                var serviceCollection = Expression.Parameter(typeof(IServiceProvider));
                var serviceRetrievers = parameters.Select(p => Expression.Call(serviceExtensions, "GetRequiredService", [p.ParameterType], serviceCollection));
                var lambda = Expression.Lambda<Func<IServiceProvider, AbstractSocketCommand>>(Expression.New(constructor, serviceRetrievers), serviceCollection);
                var func = lambda.Compile();

                _socketCommandGenerators.TryAdd(type.Name, func);
            }
        }
    }
}
