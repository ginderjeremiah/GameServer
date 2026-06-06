using Game.Api.CodeGen;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class CodeGenPathsTests
    {
        [Fact]
        public void ResolveTargetDirectory_BuildsFrontendTypesPath()
        {
            var root = Path.Combine(Path.GetTempPath(), "repo");

            var result = CodeGenPaths.ResolveTargetDirectory(root);

            var expected = Path.Combine(root, "UI", "new-svelte", "src", "lib", "api", "types");
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FindRepositoryRoot_ReturnsDirectoryContainingSolution_WhenNested()
        {
            var root = Path.Combine(Path.GetTempPath(), "codegen_root_" + Guid.NewGuid().ToString("N"));
            var nested = Path.Combine(root, "bin", "Debug", "net10.0");
            Directory.CreateDirectory(nested);
            File.WriteAllText(Path.Combine(root, "Game.sln"), "");

            try
            {
                var result = CodeGenPaths.FindRepositoryRoot(nested);

                Assert.Equal(new DirectoryInfo(root).FullName, result);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void FindRepositoryRoot_Throws_WhenSolutionNotFound()
        {
            var root = Path.Combine(Path.GetTempPath(), "codegen_noroot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                Assert.Throws<InvalidOperationException>(() => CodeGenPaths.FindRepositoryRoot(root));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
