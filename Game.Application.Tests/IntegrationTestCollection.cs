using Game.TestInfrastructure.Fixtures;
using Xunit;

namespace Game.Application.Tests
{
    [CollectionDefinition("Integration")]
    public class IntegrationTestCollection : ICollectionFixture<IntegrationTestContainers>
    {
    }
}
