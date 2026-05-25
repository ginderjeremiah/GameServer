using Game.Api.CodeGen;
using Game.Api.CodeGen.Data;
using Game.Api.CodeGen.Writers;

namespace Game.Api.Tests.CodeGen
{
    [TestClass]
    public class ApiMapWriterTests
    {
        private string _tempDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "codegen_test_" + Guid.NewGuid().ToString("N"));
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
        public void WriteApiMap_CreatesFile()
        {
            var writer = new ApiMapWriter(_tempDir);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], "// Auto-generated");

            Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "api-type-map.ts")));
        }

        [TestMethod]
        public void WriteApiMap_ContainsResponseTypes()
        {
            var writer = new ApiMapWriter(_tempDir);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));
            Assert.IsTrue(content.Contains("export type ApiResponseTypes = {"));
            Assert.IsTrue(content.Contains("'Test': ISimpleModel;"));
        }

        [TestMethod]
        public void WriteApiMap_ContainsRequestTypes()
        {
            var writer = new ApiMapWriter(_tempDir);
            var method = typeof(TestController).GetMethod("PostData")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test/PostData", IsGet = false };

            writer.WriteApiMap([endpoint], "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));
            Assert.IsTrue(content.Contains("export type ApiRequestTypes = {"));
            Assert.IsTrue(content.Contains("'Test/PostData': ISimpleModel;"));
        }

        [TestMethod]
        public void WriteApiMap_EndpointsOrderedAlphabetically()
        {
            var writer = new ApiMapWriter(_tempDir);
            var method1 = typeof(TestController).GetMethod("GetSimple")!;
            var method2 = typeof(TestController).GetMethod("PostData")!;
            var endpoints = new[]
            {
                new EndpointMetadata(method2) { Endpoint = "Zebra", IsGet = false },
                new EndpointMetadata(method1) { Endpoint = "Alpha", IsGet = true },
            };

            writer.WriteApiMap(endpoints, "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));
            var alphaIndex = content.IndexOf("'Alpha'");
            var zebraIndex = content.IndexOf("'Zebra'");
            Assert.IsTrue(alphaIndex < zebraIndex);
        }

        [TestMethod]
        public void WriteApiMap_ContainsImports_WhenTypesNeedInterface()
        {
            var writer = new ApiMapWriter(_tempDir);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));
            Assert.IsTrue(content.Contains("import type"));
            Assert.IsTrue(content.Contains("ISimpleModel"));
        }

        [TestMethod]
        public void WriteApiMap_ContainsHelperTypes()
        {
            var writer = new ApiMapWriter(_tempDir);
            writer.WriteApiMap([], "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));
            Assert.IsTrue(content.Contains("export type ApiEndpoint = keyof ApiResponseTypes;"));
            Assert.IsTrue(content.Contains("export type ApiEndpointWithRequest = keyof ApiRequestTypes;"));
            Assert.IsTrue(content.Contains("export type ApiResponseType = ApiResponseTypes[ApiEndpoint];"));
        }

        [TestMethod]
        public void WriteApiMap_DoesNotRewrite_WhenContentSame()
        {
            var writer = new ApiMapWriter(_tempDir);
            writer.WriteApiMap([], "// Auto-generated");

            var firstWriteTime = File.GetLastWriteTimeUtc(Path.Combine(_tempDir, "api-type-map.ts"));

            Thread.Sleep(50);
            writer.WriteApiMap([], "// Auto-generated");

            var secondWriteTime = File.GetLastWriteTimeUtc(Path.Combine(_tempDir, "api-type-map.ts"));
            Assert.AreEqual(firstWriteTime, secondWriteTime);
        }

        [TestMethod]
        public void WriteApiMap_IncludesAutoGeneratedComment_AtStartOfFile()
        {
            var testComment = "/* Generated: 2024-01-01 */";
            var writer = new ApiMapWriter(_tempDir);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], testComment);

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));
            Assert.IsTrue(content.StartsWith(testComment), $"Expected content to start with '{testComment}'");
        }

        [TestMethod]
        public void WriteApiMap_UsesProvidedComment_NotDefault()
        {
            var customComment = "// Custom API Map Comment";
            var defaultComment = "// Auto-generated";
            var writer = new ApiMapWriter(_tempDir);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], customComment);

            var content = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));
            Assert.IsTrue(content.StartsWith(customComment));
            Assert.IsFalse(content.StartsWith(defaultComment));
        }

        [TestMethod]
        public void WriteApiMap_DifferentComments_GenerateDifferentFiles()
        {
            var comment1 = "/* Build 1 */";
            var comment2 = "/* Build 2 */";
            var writer = new ApiMapWriter(_tempDir);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            // First write with comment1
            writer.WriteApiMap([endpoint], comment1);
            var content1 = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));

            // Second write with comment2
            writer.WriteApiMap([endpoint], comment2);
            var content2 = File.ReadAllText(Path.Combine(_tempDir, "api-type-map.ts"));

            Assert.IsTrue(content1.StartsWith(comment1));
            Assert.IsTrue(content2.StartsWith(comment2));
            Assert.AreNotEqual(content1, content2);
        }
    }
}
