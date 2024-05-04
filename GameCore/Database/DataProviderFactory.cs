using GameCore.Database.Interfaces;
using GameCore.Database.SqlServer;
using Microsoft.Extensions.DependencyInjection;

namespace GameCore.Database
{
    public static class DataProviderFactory
    {
        public static void AddDataProviderService(IServiceCollection services)
        {
            services.AddTransient<IDataProvider, SqlServerProvider>();
        }
    }
}
