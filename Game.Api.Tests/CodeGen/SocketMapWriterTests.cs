using Game.Api.CodeGen.Data;
using Game.Api.CodeGen.Writers;

namespace Game.Api.Tests.CodeGen
{
    [TestClass]
    public class SocketMapWriterTests
    {
        private string _tempDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "codegen_socket_test_" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [TestMethod]
        public void WriteSocketMap_CreatesFile()
        {
            var writer = new SocketMapWriter(_tempDir);
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandWithResponse));

            writer.WriteSocketMap([metadata]);

            Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "api-socket-type-map.ts")));
        }

        [TestMethod]
        public void WriteSocketMap_ContainsResponseTypes()
        {
            var writer = new SocketMapWriter(_tempDir);
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandWithResponse));

            writer.WriteSocketMap([metadata]);

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-socket-type-map.ts"));
            Assert.IsTrue(content.Contains("export type ApiSocketResponseTypes = {"));
            Assert.IsTrue(content.Contains($"'{metadata.CommandName}': ISimpleModel;"));
        }

        [TestMethod]
        public void WriteSocketMap_ContainsRequestTypes()
        {
            var writer = new SocketMapWriter(_tempDir);
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandFull));

            writer.WriteSocketMap([metadata]);

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-socket-type-map.ts"));
            Assert.IsTrue(content.Contains("export type ApiSocketRequestTypes = {"));
            Assert.IsTrue(content.Contains($"'{metadata.CommandName}': ISocketParamModel;"));
        }

        [TestMethod]
        public void WriteSocketMap_NoParamCommand_ExcludedFromRequests()
        {
            var writer = new SocketMapWriter(_tempDir);
            var metadata = new SocketCommandMetadata(typeof(TestSocketCommandBasic));

            writer.WriteSocketMap([metadata]);

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-socket-type-map.ts"));
            // Should appear in response types (with undefined since no response descriptor)
            Assert.IsTrue(content.Contains($"'{metadata.CommandName}': undefined;"));
            // Request section should be empty (no entry for this command)
            var requestSection = content[(content.IndexOf("ApiSocketRequestTypes") + 1)..];
            Assert.IsFalse(requestSection.Contains(metadata.CommandName));
        }

        [TestMethod]
        public void WriteSocketMap_ContainsHelperTypes()
        {
            var writer = new SocketMapWriter(_tempDir);
            writer.WriteSocketMap([]);

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-socket-type-map.ts"));
            Assert.IsTrue(content.Contains("export type ApiSocketCommand = keyof ApiSocketResponseTypes;"));
            Assert.IsTrue(content.Contains("export type ApiSocketCommandWithRequest = keyof ApiSocketRequestTypes;"));
            Assert.IsTrue(content.Contains("export type ApiSocketCommandNoRequest = Exclude<ApiSocketCommand, ApiSocketCommandWithRequest>;"));
            Assert.IsTrue(content.Contains("export type ApiSocketResponseType = ApiSocketResponseTypes[ApiSocketCommand];"));
        }

        [TestMethod]
        public void WriteSocketMap_CommandsOrderedAlphabetically()
        {
            var writer = new SocketMapWriter(_tempDir);
            var metadata = new List<SocketCommandMetadata>
            {
                new(typeof(TestSocketCommandWithResponse)),
                new(typeof(TestSocketCommandBasic)),
            };

            writer.WriteSocketMap(metadata);

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-socket-type-map.ts"));
            var basicIndex = content.IndexOf("TestSocketCommandBasic");
            var responseIndex = content.IndexOf("TestSocketCommandWithResponse");
            Assert.IsTrue(basicIndex < responseIndex);
        }
    }
}
