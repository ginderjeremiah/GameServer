using Game.Api.Sockets.Commands;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Game.Api.Services
{
    public class SocketCommandFactory
    {
        private static readonly ConcurrentDictionary<string, Func<IServiceProvider, AbstractSocketCommand>> _socketCommandGenerators = [];
        private static readonly Task _registerCommandGeneratorsTask = Task.Run(RegisterSocketCommandGenerators);

        private readonly IServiceProvider _serviceProvider;

        public SocketCommandFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<AbstractSocketCommand> CreateCommand(SocketCommandInfo commandInfo)
        {
            await _registerCommandGeneratorsTask;
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

        private static void RegisterSocketCommandGenerators()
        {
            var assembly = typeof(SocketCommandFactory).Assembly;
            var types = assembly.GetTypes();
            var serviceExtensions = typeof(ServiceProviderServiceExtensions);
            foreach (var type in types.Where(t => t.IsAssignableTo(typeof(AbstractSocketCommand)) && !t.IsAbstract))
            {
                var constructor = type.GetConstructors().First();
                var serviceDependencies = constructor.GetParameters();
                var serviceProvider = Expression.Parameter(typeof(IServiceProvider));
                var serviceInjectors = serviceDependencies.Select(p => Expression.Call(serviceExtensions, "GetRequiredService", [p.ParameterType], serviceProvider));
                var commandGenerator = Expression.Lambda<Func<IServiceProvider, AbstractSocketCommand>>(Expression.New(constructor, serviceInjectors), serviceProvider);
                var compiledGenerator = commandGenerator.Compile();

                _socketCommandGenerators.TryAdd(type.Name, compiledGenerator);
            }
        }
    }
}
