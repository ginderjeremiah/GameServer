using Game.Api.Sockets.Commands;
using Xunit;

namespace Game.Api.Tests.Unit
{
    public class GetReferenceDataVersionsTests
    {
        /// <summary>A minimal real <see cref="IReferenceDataCommand"/> standing in for a Get* command.</summary>
        private sealed class StubReferenceDataCommand : IReferenceDataCommand
        {
            public required string Name { get; init; }
            public required string Version { get; init; }

            public string ComputeVersion() => Version;
        }

        [Fact]
        public void HandleExecute_ReturnsOneVersionPerReferenceDataCommand()
        {
            var command = new GetReferenceDataVersions(
            [
                new StubReferenceDataCommand { Name = "GetZones", Version = "z1" },
                new StubReferenceDataCommand { Name = "GetEnemies", Version = "e1" }
            ]);

            // The command ignores the socket context entirely.
            var response = command.HandleExecute(null!);

            Assert.Null(response.Error);
            Assert.NotNull(response.Data);
            Assert.Collection(response.Data,
                v => Assert.Equal(("GetEnemies", "e1"), (v.Command, v.Version)),
                v => Assert.Equal(("GetZones", "z1"), (v.Command, v.Version)));
        }

        [Fact]
        public void HandleExecute_OrdersVersionsByCommandName()
        {
            var command = new GetReferenceDataVersions(
            [
                new StubReferenceDataCommand { Name = "GetZones", Version = "z" },
                new StubReferenceDataCommand { Name = "GetAttributes", Version = "a" },
                new StubReferenceDataCommand { Name = "GetItems", Version = "i" }
            ]);

            var response = command.HandleExecute(null!);

            Assert.Equal(
                ["GetAttributes", "GetItems", "GetZones"],
                response.Data.Select(v => v.Command));
        }

        [Fact]
        public void HandleExecute_ReturnsEmptyWhenNoReferenceDataCommands()
        {
            var command = new GetReferenceDataVersions([]);

            var response = command.HandleExecute(null!);

            Assert.Null(response.Error);
            Assert.Empty(response.Data);
        }
    }
}
