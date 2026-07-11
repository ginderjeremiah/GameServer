using Game.Api.Services;
using Game.Api.Sockets.Commands;
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

        [Fact]
        public void RegisterSocketCommandGenerators_AllConcreteCommandsHaveExactlyOnePublicConstructor()
        {
            // Registration binds each command via GetConstructors().Single(), so a concrete command
            // gaining a second public constructor would throw at registration. Assert the invariant
            // directly so the guard can't silently regress.
            var concreteCommands = typeof(SocketCommandFactory).Assembly.GetTypes()
                .Where(t => t.IsAssignableTo(typeof(AbstractSocketCommand)) && !t.IsAbstract)
                .ToList();

            Assert.NotEmpty(concreteCommands);
            Assert.All(concreteCommands, command => Assert.Single(command.GetConstructors()));
        }

        [Theory]
        [InlineData("ChallengeCompleted")]
        [InlineData("ProficiencyXpGained")]
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

        [Theory]
        [InlineData("ChallengeCompleted")]
        [InlineData("ProficiencyXpGained")]
        public void IsReplayable_ReturnsTrue_ForCommandsThatOptIntoReplay(string commandName)
        {
            var factory = new SocketCommandFactory();

            Assert.True(factory.IsReplayable(commandName));
        }

        [Theory]
        [InlineData("SocketReplaced")]
        [InlineData("ServerCommandFailed")]
        [InlineData("GetZones")]
        [InlineData("NonExistentCommand")]
        public void IsReplayable_ReturnsFalse_ForSessionLifecycleCommandsAndNonServerInitiatedOrUnknownCommands(string commandName)
        {
            var factory = new SocketCommandFactory();

            Assert.False(factory.IsReplayable(commandName));
        }

        [Theory]
        [InlineData("GetZones")]
        [InlineData("DefeatEnemy")]
        [InlineData("SocketReplaced")]
        public void IsKnownCommand_ReturnsTrue_ForRegisteredCommands(string commandName)
        {
            var factory = new SocketCommandFactory();

            Assert.True(factory.IsKnownCommand(commandName));
        }

        [Theory]
        [InlineData("NonExistentCommand")]
        [InlineData("")]
        public void IsKnownCommand_ReturnsFalse_ForUnregisteredCommands(string commandName)
        {
            var factory = new SocketCommandFactory();

            Assert.False(factory.IsKnownCommand(commandName));
        }
    }
}
