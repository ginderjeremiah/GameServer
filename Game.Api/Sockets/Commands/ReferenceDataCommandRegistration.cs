using Microsoft.Extensions.DependencyInjection;

namespace Game.Api.Sockets.Commands
{
    /// <summary>
    /// Registers the reference-data socket commands as <see cref="IReferenceDataCommand"/> so
    /// <see cref="GetReferenceDataVersions"/> can resolve and version all of them via an injected
    /// <c>IEnumerable&lt;IReferenceDataCommand&gt;</c>. (Commands are otherwise instantiated by
    /// <see cref="Services.SocketCommandFactory"/> rather than the container; this registration
    /// exists solely to enumerate them for versioning.)
    /// </summary>
    public static class ReferenceDataCommandRegistration
    {
        public static IServiceCollection AddReferenceDataCommands(this IServiceCollection services)
        {
            var commandTypes = typeof(ReferenceDataCommandRegistration).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && type.IsAssignableTo(typeof(IReferenceDataCommand)));

            foreach (var commandType in commandTypes)
            {
                services.AddScoped(typeof(IReferenceDataCommand), commandType);
            }

            return services;
        }
    }
}
