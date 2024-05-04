using DataAccess;
using GameCore.Logging.Interfaces;
using GameServer;
using GameTests.Mocks.DataAccess;
using GameTests.Mocks.GameCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace GameTests.Mocks.GameServer
{
    internal class ApiAppFactory : WebApplicationFactory<Startup>
    {
        public MockLogger Logger { get; set; } = new MockLogger();
        public MockRepositoryManager Repositories { get; set; } = new MockRepositoryManager();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IApiLogger>(Logger);
                services.AddSingleton<IRepositoryManager>(Repositories);
            });
        }
    }
}
