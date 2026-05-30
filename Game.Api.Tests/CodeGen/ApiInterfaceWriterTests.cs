using Game.Api.CodeGen;
using Game.Api.CodeGen.Writers;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class ApiInterfaceWriterTests : IDisposable
    {
        private readonly string _tempDir;

        public ApiInterfaceWriterTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "codegen_iface_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }

        [Fact]
        public void WriteApiInterfaces_CreatesInterfacesDirectory()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor], "// Auto-generated");

            Assert.True(Directory.Exists(Path.Combine(_tempDir, "interfaces")));
        }

        [Fact]
        public void WriteApiInterfaces_CreatesIndexFile()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor], "// Auto-generated");

            Assert.True(File.Exists(Path.Combine(_tempDir, "index.ts")));
        }

        [Fact]
        public void WriteApiInterfaces_WritesExportStatement()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor], "// Auto-generated");

            var indexContent = File.ReadAllText(Path.Combine(_tempDir, "index.ts"));
            Assert.Contains("export * from", indexContent);
        }

        [Fact]
        public void WriteApiInterfaces_InterfaceContainsProperties()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor], "// Auto-generated");

            var files = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            Assert.True(files.Length > 0);

            var content = File.ReadAllText(files[0]);
            Assert.Contains("export interface ISimpleModel {", content);
            Assert.Contains("id: number;", content);
            Assert.Contains("name: string;", content);
            Assert.Contains("isActive: boolean;", content);
        }

        [Fact]
        public void WriteApiInterfaces_EnumType_WritesEnumDefinition()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<ModelWithEnum>();

            writer.WriteApiInterfaces([descriptor], "// Auto-generated");

            var enumPath = Path.Combine(_tempDir, "enums.ts");
            Assert.True(File.Exists(enumPath));

            var content = File.ReadAllText(enumPath);
            Assert.Contains("export enum TestEnum {", content);
            Assert.Contains("None = 0,", content);
            Assert.Contains("Active = 1,", content);
            Assert.Contains("Inactive = 2,", content);
            Assert.Contains("Pending = 3,", content);
        }

        [Fact]
        public void WriteApiInterfaces_NestedClass_WritesImports()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<NestedModel>();

            writer.WriteApiInterfaces([descriptor], "// Auto-generated");

            var files = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            var content = string.Join("\n", files.Select(File.ReadAllText));

            Assert.Contains("INestedModel", content);
            Assert.Contains("ISimpleModel", content);
        }

        [Fact]
        public void WriteApiInterfaces_RemovesStaleFiles()
        {
            var interfacesDir = Path.Combine(_tempDir, "interfaces");
            Directory.CreateDirectory(interfacesDir);
            File.WriteAllText(Path.Combine(interfacesDir, "stale-file.ts"), "old content");

            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor], "// Auto-generated");

            Assert.False(File.Exists(Path.Combine(interfacesDir, "stale-file.ts")));
        }

        [Fact]
        public void WriteApiInterfaces_GroupsByNamespace()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor1 = GetDescriptorForClass<SimpleModel>();
            var descriptor2 = GetDescriptorForClass<NestedModel>();

            // Both are in same namespace (Game.Api.Tests.CodeGen), so should go to same file
            writer.WriteApiInterfaces([descriptor1, descriptor2], "// Auto-generated");

            var files = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            Assert.Single(files);
            var content = File.ReadAllText(files[0]);
            Assert.Contains("ISimpleModel", content);
            Assert.Contains("INestedModel", content);
        }

        [Fact]
        public void WriteApiInterfaces_IncludesAutoGeneratedComment_InInterfaceFiles()
        {
            var testComment = "// Custom Auto-generated Comment";
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor], testComment);

            var files = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            Assert.True(files.Length > 0);

            var content = File.ReadAllText(files[0]);
            Assert.StartsWith(testComment, content);
        }

        [Fact]
        public void WriteApiInterfaces_UsesProvidedComment_NotDefault()
        {
            var customComment = "/* Custom Generated */";
            var defaultComment = "// Auto-generated";
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor], customComment);

            var files = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            var content = File.ReadAllText(files[0]);
            Assert.StartsWith(customComment, content);
            Assert.False(content.StartsWith(defaultComment));
        }

        [Fact]
        public void WriteApiInterfaces_DifferentComments_GenerateDifferentFiles()
        {
            var comment1 = "/* Version 1 */";
            var comment2 = "/* Version 2 */";
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            // First write with comment1
            writer.WriteApiInterfaces([descriptor], comment1);
            var files1 = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            var content1 = File.ReadAllText(files1[0]);

            // Second write with comment2
            writer.WriteApiInterfaces([descriptor], comment2);
            var files2 = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            var content2 = File.ReadAllText(files2[0]);

            Assert.StartsWith(comment1, content1);
            Assert.StartsWith(comment2, content2);
            Assert.NotEqual(content1, content2);
        }

        private static CodeGenTypeDescriptor GetDescriptorForClass<T>()
        {
            var prop = typeof(Wrapper<T>).GetProperty("Value")!;
            return new CodeGenTypeDescriptor(prop);
        }
    }

    public class Wrapper<T>
    {
        public T Value { get; set; } = default!;
    }
}
