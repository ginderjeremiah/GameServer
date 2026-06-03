using Game.TestInfrastructure.Fixtures;
using Xunit;

namespace Game.Application.Tests.Fixtures
{
    public class PreexistingContainerInfoTests : IDisposable
    {
        private readonly string _root;

        public PreexistingContainerInfoTests()
        {
            _root = Path.Combine(Path.GetTempPath(), $"container-info-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            Directory.Delete(_root, recursive: true);
            GC.SuppressFinalize(this);
        }

        [Fact]
        public void TryLoad_ReturnsNull_WhenMarkerIsAbsent()
        {
            var result = PreexistingContainerInfo.TryLoad(_root);

            Assert.Null(result);
        }

        [Fact]
        public void TryLoad_ParsesConnectionStrings_WhenMarkerExistsInStartDirectory()
        {
            WriteMarker(_root,
                postgres: "Host=localhost;Port=5499;Database=game_test;Username=test;Password=test",
                redis: "localhost:6399");

            var result = PreexistingContainerInfo.TryLoad(_root);

            Assert.NotNull(result);
            Assert.Equal("Host=localhost;Port=5499;Database=game_test;Username=test;Password=test", result.Postgres);
            Assert.Equal("localhost:6399", result.Redis);
        }

        [Fact]
        public void TryLoad_WalksUpToAncestorDirectory_ToFindMarker()
        {
            WriteMarker(_root, postgres: "Host=localhost", redis: "localhost:6399");
            var nested = Directory.CreateDirectory(Path.Combine(_root, "bin", "Debug", "net10.0")).FullName;

            var result = PreexistingContainerInfo.TryLoad(nested);

            Assert.NotNull(result);
            Assert.Equal("Host=localhost", result.Postgres);
        }

        private static void WriteMarker(string directory, string postgres, string redis)
        {
            var path = Path.Combine(directory, PreexistingContainerInfo.MarkerFileName);
            File.WriteAllText(path, $$"""
                {
                  "postgres": "{{postgres}}",
                  "redis": "{{redis}}"
                }
                """);
        }
    }
}
