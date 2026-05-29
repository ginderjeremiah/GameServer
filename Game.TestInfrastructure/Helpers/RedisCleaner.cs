using StackExchange.Redis;

namespace Game.TestInfrastructure.Helpers
{
    public static class RedisCleaner
    {
        public static async Task FlushAsync(string connectionString)
        {
            var options = ConfigurationOptions.Parse(connectionString);
            options.AllowAdmin = true;
            var multiplexer = await ConnectionMultiplexer.ConnectAsync(options);
            var server = multiplexer.GetServers().First();
            await server.FlushDatabaseAsync();
            await multiplexer.DisposeAsync();
        }
    }
}
