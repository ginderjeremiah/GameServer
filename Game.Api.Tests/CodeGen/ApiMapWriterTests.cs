using Game.Api.CodeGen;
using Game.Api.CodeGen.Data;
using Game.Api.CodeGen.Writers;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class ApiMapWriterTests : IDisposable
    {
        private readonly CodeGenOptions _options;

        public ApiMapWriterTests()
        {
            _options = new CodeGenOptions
            {
                TargetDirectory = Path.Combine(Path.GetTempPath(), "codegen_test_" + Guid.NewGuid().ToString("N")),
                NewLine = "\n"
            };
        }

        public void Dispose()
        {
            if (Directory.Exists(_options.TargetDirectory))
            {
                Directory.Delete(_options.TargetDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteApiMap_CreatesFile()
        {
            var writer = new ApiMapWriter(_options);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], "// Auto-generated");

            Assert.True(File.Exists(Path.Combine(_options.TargetDirectory, "api-type-map.ts")));
        }

        [Fact]
        public void WriteApiMap_ContainsResponseTypes()
        {
            var writer = new ApiMapWriter(_options);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));
            Assert.Contains("export type ApiResponseTypes = {", content);
            Assert.Contains("'Test': ISimpleModel;", content);
        }

        [Fact]
        public void WriteApiMap_ContainsRequestTypes()
        {
            var writer = new ApiMapWriter(_options);
            var method = typeof(TestController).GetMethod("PostData")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test/PostData", IsGet = false };

            writer.WriteApiMap([endpoint], "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));
            Assert.Contains("export type ApiRequestTypes = {", content);
            Assert.Contains("'Test/PostData': ISimpleModel;", content);
        }

        [Fact]
        public void WriteApiMap_EndpointsOrderedAlphabetically()
        {
            var writer = new ApiMapWriter(_options);
            var method1 = typeof(TestController).GetMethod("GetSimple")!;
            var method2 = typeof(TestController).GetMethod("PostData")!;
            var endpoints = new[]
            {
                new EndpointMetadata(method2) { Endpoint = "Zebra", IsGet = false },
                new EndpointMetadata(method1) { Endpoint = "Alpha", IsGet = true },
            };

            writer.WriteApiMap(endpoints, "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));
            var alphaIndex = content.IndexOf("'Alpha'");
            var zebraIndex = content.IndexOf("'Zebra'");
            Assert.True(alphaIndex < zebraIndex);
        }

        [Fact]
        public void WriteApiMap_ContainsImports_WhenTypesNeedInterface()
        {
            var writer = new ApiMapWriter(_options);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));
            Assert.Contains("import type", content);
            Assert.Contains("ISimpleModel", content);
        }

        [Fact]
        public void WriteApiMap_ContainsHelperTypes()
        {
            var writer = new ApiMapWriter(_options);
            writer.WriteApiMap([], "// Auto-generated");

            var content = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));
            Assert.Contains("export type ApiEndpoint = keyof ApiResponseTypes;", content);
            Assert.Contains("export type ApiEndpointWithRequest = keyof ApiRequestTypes;", content);
            Assert.Contains("export type ApiResponseType = ApiResponseTypes[ApiEndpoint];", content);
        }

        [Fact]
        public void WriteApiMap_DoesNotRewrite_WhenContentSame()
        {
            var writer = new ApiMapWriter(_options);
            writer.WriteApiMap([], "// Auto-generated");

            var firstWriteTime = File.GetLastWriteTimeUtc(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));

            Thread.Sleep(50);
            writer.WriteApiMap([], "// Auto-generated");

            var secondWriteTime = File.GetLastWriteTimeUtc(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));
            Assert.Equal(firstWriteTime, secondWriteTime);
        }

        [Fact]
        public void WriteApiMap_IncludesAutoGeneratedComment_AtStartOfFile()
        {
            var testComment = "/* Generated: 2024-01-01 */";
            var writer = new ApiMapWriter(_options);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], testComment);

            var content = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));
            Assert.StartsWith(testComment, content);
        }

        [Fact]
        public void WriteApiMap_UsesProvidedComment_NotDefault()
        {
            var customComment = "// Custom API Map Comment";
            var defaultComment = "// Auto-generated";
            var writer = new ApiMapWriter(_options);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            writer.WriteApiMap([endpoint], customComment);

            var content = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));
            Assert.StartsWith(customComment, content);
            Assert.False(content.StartsWith(defaultComment));
        }

        [Fact]
        public void WriteApiMap_DifferentComments_GenerateDifferentFiles()
        {
            var comment1 = "/* Build 1 */";
            var comment2 = "/* Build 2 */";
            var writer = new ApiMapWriter(_options);
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test", IsGet = true };

            // First write with comment1
            writer.WriteApiMap([endpoint], comment1);
            var content1 = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));

            // Second write with comment2
            writer.WriteApiMap([endpoint], comment2);
            var content2 = File.ReadAllText(Path.Combine(_options.TargetDirectory, "api-type-map.ts"));

            Assert.StartsWith(comment1, content1);
            Assert.StartsWith(comment2, content2);
            Assert.NotEqual(content1, content2);
        }
    }
}
