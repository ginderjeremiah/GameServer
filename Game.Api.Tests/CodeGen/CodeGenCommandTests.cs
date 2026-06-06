using Game.Api.CodeGen;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class CodeGenCommandTests : IDisposable
    {
        private readonly string _outputDirectory;

        public CodeGenCommandTests()
        {
            _outputDirectory = Path.Combine(Path.GetTempPath(), "codegen_cmd_" + Guid.NewGuid().ToString("N"));
        }

        public void Dispose()
        {
            if (Directory.Exists(_outputDirectory))
            {
                Directory.Delete(_outputDirectory, recursive: true);
            }
        }

        [Theory]
        [InlineData("codegen", true)]
        [InlineData("CODEGEN", true)]
        [InlineData("serve", false)]
        public void Matches_DetectsCommandVerb(string arg, bool expected)
        {
            Assert.Equal(expected, CodeGenCommand.Matches([arg]));
        }

        [Fact]
        public void Matches_IsFalse_WhenNoArgs()
        {
            Assert.False(CodeGenCommand.Matches([]));
        }

        [Fact]
        public void Run_GeneratesClientFiles_IntoProvidedDirectory_WithoutWebHost()
        {
            // Exercises the full reflection-based generation against the real API assembly, with no
            // web host, database, or cache — proving codegen is runnable in restricted environments.
            CodeGenCommand.Run(["codegen", _outputDirectory]);

            Assert.True(File.Exists(Path.Combine(_outputDirectory, "api-type-map.ts")));
            Assert.True(File.Exists(Path.Combine(_outputDirectory, "api-socket-type-map.ts")));
            Assert.True(File.Exists(Path.Combine(_outputDirectory, "index.ts")));
            Assert.True(Directory.Exists(Path.Combine(_outputDirectory, "interfaces")));
        }
    }
}
