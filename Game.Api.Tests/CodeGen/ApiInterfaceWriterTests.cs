using Game.Api.CodeGen;
using Game.Api.CodeGen.Writers;

namespace Game.Api.Tests.CodeGen
{
    [TestClass]
    public class ApiInterfaceWriterTests
    {
        private string _tempDir = null!;

        [TestInitialize]
        public void Setup()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "codegen_iface_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
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
        public void WriteApiInterfaces_CreatesInterfacesDirectory()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor]);

            Assert.IsTrue(Directory.Exists(Path.Combine(_tempDir, "interfaces")));
        }

        [TestMethod]
        public void WriteApiInterfaces_CreatesIndexFile()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor]);

            Assert.IsTrue(File.Exists(Path.Combine(_tempDir, "index.ts")));
        }

        [TestMethod]
        public void WriteApiInterfaces_WritesExportStatement()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor]);

            var indexContent = File.ReadAllText(Path.Combine(_tempDir, "index.ts"));
            Assert.IsTrue(indexContent.Contains("export * from"));
        }

        [TestMethod]
        public void WriteApiInterfaces_InterfaceContainsProperties()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor]);

            var files = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            Assert.IsTrue(files.Length > 0);

            var content = File.ReadAllText(files[0]);
            Assert.IsTrue(content.Contains("export interface ISimpleModel {"));
            Assert.IsTrue(content.Contains("id: number;"));
            Assert.IsTrue(content.Contains("name: string;"));
            Assert.IsTrue(content.Contains("isActive: boolean;"));
        }

        [TestMethod]
        public void WriteApiInterfaces_EnumType_WritesEnumDefinition()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<ModelWithEnum>();

            writer.WriteApiInterfaces([descriptor]);

            var enumPath = Path.Combine(_tempDir, "enums.ts");
            Assert.IsTrue(File.Exists(enumPath));

            var content = File.ReadAllText(enumPath);
            Assert.IsTrue(content.Contains("export enum TestEnum {"));
            Assert.IsTrue(content.Contains("None = 0,"));
            Assert.IsTrue(content.Contains("Active = 1,"));
            Assert.IsTrue(content.Contains("Inactive = 2,"));
            Assert.IsTrue(content.Contains("Pending = 3,"));
        }

        [TestMethod]
        public void WriteApiInterfaces_NestedClass_WritesImports()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<NestedModel>();

            writer.WriteApiInterfaces([descriptor]);

            var files = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            var content = string.Join("\n", files.Select(File.ReadAllText));

            Assert.IsTrue(content.Contains("INestedModel"));
            Assert.IsTrue(content.Contains("ISimpleModel"));
        }

        [TestMethod]
        public void WriteApiInterfaces_RemovesStaleFiles()
        {
            var interfacesDir = Path.Combine(_tempDir, "interfaces");
            Directory.CreateDirectory(interfacesDir);
            File.WriteAllText(Path.Combine(interfacesDir, "stale-file.ts"), "old content");

            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor = GetDescriptorForClass<SimpleModel>();

            writer.WriteApiInterfaces([descriptor]);

            Assert.IsFalse(File.Exists(Path.Combine(interfacesDir, "stale-file.ts")));
        }

        [TestMethod]
        public void WriteApiInterfaces_GroupsByNamespace()
        {
            var writer = new ApiInterfaceWriter(_tempDir);
            var descriptor1 = GetDescriptorForClass<SimpleModel>();
            var descriptor2 = GetDescriptorForClass<NestedModel>();

            // Both are in same namespace (Game.Api.Tests.CodeGen), so should go to same file
            writer.WriteApiInterfaces([descriptor1, descriptor2]);

            var files = Directory.GetFiles(Path.Combine(_tempDir, "interfaces"), "*.ts");
            Assert.AreEqual(1, files.Length);
            var content = File.ReadAllText(files[0]);
            Assert.IsTrue(content.Contains("ISimpleModel"));
            Assert.IsTrue(content.Contains("INestedModel"));
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
