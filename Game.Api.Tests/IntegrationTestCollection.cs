using Game.TestInfrastructure.Fixtures;
using Xunit;

namespace Game.Api.Tests
{
    [CollectionDefinition("Integration")]
    public class IntegrationTestCollection : ICollectionFixture<IntegrationTestContainers>
    {
    }
}
