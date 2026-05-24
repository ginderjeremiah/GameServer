using Game.Api.Sockets.Commands;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Game.Api.Services
{
    public class SocketCommandFactory
    {
        private static readonly ConcurrentDictionary<string, Func<IServiceProvider, AbstractSocketCommand>> _socketCommandGenerators = [];
        private static readonly Task _registerCommandGeneratorsTask = Task.Run(RegisterSocketCommandGenerators);

        private readonly IServiceScopeFactory _scopeFactory;

        public SocketCommandFactory(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Creates the requested socket command inside a fresh DI scope.
        /// The caller is responsible for disposing the returned <see cref="IServiceScope"/>
        /// after the command has executed (and after any post-execution work such as UoW commit).
        /// TODO: Make the scope a parameter of the method to make the ownership more explicit and to allow for more flexible scope management.
        /// </summary>
        public async Task<(AbstractSocketCommand Command, IServiceScope Scope)> CreateCommand(SocketCommandInfo commandInfo)
        {
            await _registerCommandGeneratorsTask;
            if (_socketCommandGenerators.TryGetValue(commandInfo.Name, out var generator))
            {
                var scope = _scopeFactory.CreateScope();
                var command = generator(scope.ServiceProvider);
                command.SetParameters(commandInfo.Parameters);
                command.Id = commandInfo.Id;
                return (command, scope);
            }
            else
            {
                throw new InvalidOperationException($"No socket command generator found for: {commandInfo.Name}.");
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
