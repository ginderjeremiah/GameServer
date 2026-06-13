using Game.Api.Services;
using Xunit;

namespace Game.Api.Tests.Unit
{
    public class SocketCommandFactoryTests
    {
        public SocketCommandFactoryTests()
        {
            // Populates the static command registry from the assembly; idempotent across tests.
            SocketCommandFactory.RegisterSocketCommandGenerators();
        }

        [Theory]
        [InlineData("ChallengeCompleted")]
        [InlineData("SocketReplaced")]
        public void IsServerInitiatedOnly_ReturnsTrue_ForServerInitiatedCommands(string commandName)
        {
            var factory = new SocketCommandFactory();

            Assert.True(factory.IsServerInitiatedOnly(commandName));
        }

        [Theory]
        [InlineData("GetZones")]
        [InlineData("SetSelectedSkills")]
        [InlineData("DefeatEnemy")]
        [InlineData("NonExistentCommand")]
        public void IsServerInitiatedOnly_ReturnsFalse_ForClientInvokableOrUnknownCommands(string commandName)
        {
            var factory = new SocketCommandFactory();

            Assert.False(factory.IsServerInitiatedOnly(commandName));
        }
    }
}
