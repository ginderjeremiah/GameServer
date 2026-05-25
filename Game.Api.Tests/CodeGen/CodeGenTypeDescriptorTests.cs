using Game.Api.CodeGen;

namespace Game.Api.Tests.CodeGen
{
    [TestClass]
    public class CodeGenTypeDescriptorTests
    {
        [TestMethod]
        public void FromProperty_Int_SetsCorrectUnderlyingType()
        {
            var prop = typeof(SimpleModel).GetProperty("Id")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(int), descriptor.UnderlyingType);
            Assert.AreEqual("Id", descriptor.Name);
            Assert.IsFalse(descriptor.IsNullable);
            Assert.IsFalse(descriptor.IsGeneric);
            Assert.IsFalse(descriptor.IsEnum);
        }

        [TestMethod]
        public void FromProperty_String_SetsCorrectType()
        {
            var prop = typeof(SimpleModel).GetProperty("Name")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(string), descriptor.UnderlyingType);
            Assert.IsFalse(descriptor.IsNullable);
        }

        [TestMethod]
        public void FromProperty_NullableInt_UnwrapsNullable()
        {
            var prop = typeof(ModelWithNullable).GetProperty("NullableInt")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(int), descriptor.UnderlyingType);
            Assert.IsTrue(descriptor.IsNullable);
        }

        [TestMethod]
        public void FromProperty_NullableString_IsNullable()
        {
            var prop = typeof(ModelWithNullable).GetProperty("NullableName")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(string), descriptor.UnderlyingType);
            Assert.IsTrue(descriptor.IsNullable);
        }

        [TestMethod]
        public void FromProperty_NullableDateTime_UnwrapsNullable()
        {
            var prop = typeof(ModelWithNullable).GetProperty("NullableDate")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(DateTime), descriptor.UnderlyingType);
            Assert.IsTrue(descriptor.IsNullable);
        }

        [TestMethod]
        public void FromProperty_List_SetsGenericArguments()
        {
            var prop = typeof(ModelWithList).GetProperty("Items")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(List<SimpleModel>), descriptor.UnderlyingType);
            Assert.IsTrue(descriptor.IsGeneric);
            Assert.AreEqual(1, descriptor.GenericArgumentDescriptors.Count);
            Assert.AreEqual(typeof(SimpleModel), descriptor.GenericArgumentDescriptors[0].UnderlyingType);
        }

        [TestMethod]
        public void FromProperty_Enum_SetsIsEnum()
        {
            var prop = typeof(ModelWithEnum).GetProperty("Status")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(TestEnum), descriptor.UnderlyingType);
            Assert.IsTrue(descriptor.IsEnum);
        }

        [TestMethod]
        public void FromProperty_Class_PopulatesPropertyDescriptors()
        {
            var prop = typeof(NestedModel).GetProperty("Child")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(SimpleModel), descriptor.UnderlyingType);
            Assert.IsTrue(descriptor.NeedsInterface);
            Assert.AreEqual(3, descriptor.PropertyDescriptors.Count);
        }

        [TestMethod]
        public void FromParameter_SetsNameAndType()
        {
            var method = typeof(TestController).GetMethod("PostData")!;
            var param = method.GetParameters()[0];
            var descriptor = new CodeGenTypeDescriptor(param);

            Assert.AreEqual("model", descriptor.Name);
            Assert.AreEqual(typeof(SimpleModel), descriptor.UnderlyingType);
            Assert.IsFalse(descriptor.HasDefault);
        }

        [TestMethod]
        public void TypeName_NonGeneric_ReturnsName()
        {
            var prop = typeof(SimpleModel).GetProperty("Id")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual("Int32", descriptor.TypeName);
        }

        [TestMethod]
        public void TypeName_Generic_StripsBacktick()
        {
            var prop = typeof(ModelWithList).GetProperty("Items")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual("List", descriptor.TypeName);
        }

        [TestMethod]
        public void LastNamespacePart_ReturnsSnakeCased()
        {
            var prop = typeof(NestedModel).GetProperty("Child")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            // SimpleModel is in Game.Api.Tests.CodeGen namespace
            // Last part "CodeGen" → "code-gen"
            Assert.AreEqual("code-gen", descriptor.LastNamespacePart);
        }

        [TestMethod]
        public void GenericParameterPosition_Default_IsNegativeOne()
        {
            var prop = typeof(SimpleModel).GetProperty("Id")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(-1, descriptor.GenericParameterPosition);
        }

        [TestMethod]
        public void FromProperty_GenericType_SetsPropertyDescriptorsWithPositions()
        {
            var prop = typeof(GenericHolder).GetProperty("GenericProp")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.AreEqual(typeof(GenericModel<string>), descriptor.UnderlyingType);
            Assert.IsTrue(descriptor.IsGeneric);
            Assert.AreEqual(2, descriptor.PropertyDescriptors.Count);

            var valueProp = descriptor.PropertyDescriptors.First(d => d.Name == "Value");
            Assert.AreEqual(0, valueProp.GenericParameterPosition);

            var descProp = descriptor.PropertyDescriptors.First(d => d.Name == "Description");
            Assert.AreEqual(-1, descProp.GenericParameterPosition);
        }

        [TestMethod]
        public void NeedsInterface_ForString_ReturnsFalse()
        {
            var prop = typeof(SimpleModel).GetProperty("Name")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.IsFalse(descriptor.NeedsInterface);
        }

        [TestMethod]
        public void NeedsInterface_ForClass_ReturnsTrue()
        {
            var prop = typeof(NestedModel).GetProperty("Child")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.IsTrue(descriptor.NeedsInterface);
        }
    }
}
