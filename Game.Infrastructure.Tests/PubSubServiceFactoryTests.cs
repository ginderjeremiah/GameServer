using Game.Abstractions.Infrastructure;
using Game.Infrastructure.PubSub;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Game.Infrastructure.Tests
{
    /// <summary>
    /// Tests that <see cref="PubSubServiceFactory"/> fails loud on an unsupported <see cref="PubSubSystem"/>
    /// rather than silently defaulting to Redis (the fail-loud convention #453 established). The happy path
    /// (Redis) opens a real connection, so it is covered by integration tests rather than here — selecting an
    /// unknown value never reaches the multiplexer, so this stays an in-process unit test. Note that, unlike
    /// <see cref="DatabaseSystem"/>, the enum's default (0) is the valid <see cref="PubSubSystem.Redis"/> member,
    /// so only a genuinely unrecognized value can trip the guard.
    /// </summary>
    public class PubSubServiceFactoryTests
    {
        [Fact]
        public void GetPubSubService_ForUnknownPubSubSystem_ThrowsInvalidOperationException()
        {
            var options = new InfrastructureOptions
            {
                PubSubSystem = (PubSubSystem)99,
                PubSubConnectionString = "localhost"
            };

            var ex = Assert.Throws<InvalidOperationException>(
                () => PubSubServiceFactory.GetPubSubService(options, NullLoggerFactory.Instance));
            Assert.Contains("PubSubSystem", ex.Message);
        }
    }
}
