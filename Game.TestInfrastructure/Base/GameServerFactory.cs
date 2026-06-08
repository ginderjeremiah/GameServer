using Game.Api;
using Game.TestInfrastructure.Fixtures;
using Game.TestInfrastructure.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Game.TestInfrastructure.Base
{
    public class GameServerFactory : WebApplicationFactory<Startup>
    {
        private readonly IntegrationTestContainers _containers;
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly IEnumerable<ILoggerProvider> _additionalLoggerProviders;

        public GameServerFactory(
            IntegrationTestContainers containers,
            ITestOutputHelper testOutputHelper,
            IEnumerable<ILoggerProvider>? additionalLoggerProviders = null)
        {
            _containers = containers;
            _testOutputHelper = testOutputHelper;
            _additionalLoggerProviders = additionalLoggerProviders ?? [];
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["HashPepper"] = "test-pepper-value-for-integration-tests",
                    ["Jwt:SigningKey"] = TestAuthHelper.TestSigningKey,
                    ["Jwt:Issuer"] = Game.Api.Constants.SERVER_PRINCIPAL,
                    ["Jwt:Audience"] = Game.Api.Constants.SERVER_PRINCIPAL,
                    ["DataAccessOptions:DatabaseSystem"] = "1",
                    ["DataAccessOptions:EnableSensitiveLogging"] = "true",
                    ["DataAccessOptions:CacheSystem"] = "0",
                    ["DataAccessOptions:PubSubSystem"] = "0",
                    ["DataAccessOptions:DbConnectionString"] = _containers.DbConnectionString,
                    ["DataAccessOptions:CacheConnectionString"] = _containers.CacheConnectionString,
                    ["DataAccessOptions:PubSubConnectionString"] = _containers.PubSubConnectionString,
                });
            });

            builder.ConfigureServices(services =>
            {
                services.AddLogging(loggingBuilder =>
                {
                    loggingBuilder.ClearProviders().AddProvider(new XunitLoggerProvider(_testOutputHelper));
                    foreach (var provider in _additionalLoggerProviders)
                    {
                        loggingBuilder.AddProvider(provider);
                    }
                });

                var hostedServiceDescriptors = services
                    .Where(d => d.ServiceType == typeof(IHostedService))
                    .ToList();

                foreach (var descriptor in hostedServiceDescriptors)
                {
                    services.Remove(descriptor);
                }
            });
        }
    }
}
