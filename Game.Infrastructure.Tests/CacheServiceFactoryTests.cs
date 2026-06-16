using Game.Abstractions.Infrastructure;
using Game.Infrastructure.Cache;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Tests that <see cref="CacheServiceFactory"/> fails loud on an unsupported <see cref="CacheSystem"/>
    /// rather than silently defaulting to Redis (the fail-loud convention #453 established). The happy path
    /// (Redis) opens a real connection, so it is covered by integration tests rather than here — selecting an
    /// unknown value never reaches the multiplexer, so this stays an in-process unit test. Note that, unlike
    /// <see cref="DatabaseSystem"/>, the enum's default (0) is the valid <see cref="CacheSystem.Redis"/> member,
    /// so only a genuinely unrecognized value can trip the guard.
    /// </summary>
    public class CacheServiceFactoryTests
    {
        [Fact]
        public void GetCacheService_ForUnknownCacheSystem_ThrowsInvalidOperationException()
        {
            var options = new InfrastructureOptions
            {
                CacheSystem = (CacheSystem)99,
                CacheConnectionString = "localhost"
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => CacheServiceFactory.GetCacheService(options, NullLoggerFactory.Instance));
            Assert.Contains("CacheSystem", ex.Message);
        }
    }
}
