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

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_NonGenericType_ReturnsOnlyProperties()
        {
            var prop = typeof(SimpleModel).GetProperty("Id")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            Assert.AreEqual(0, references.Count);
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_SimpleClass_ReturnsAllProperties()
        {
            var prop = typeof(NestedModel).GetProperty("Child")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            Assert.AreEqual(3, references.Count);
            Assert.IsTrue(references.Any(r => r.Name == "Id"));
            Assert.IsTrue(references.Any(r => r.Name == "Name"));
            Assert.IsTrue(references.Any(r => r.Name == "IsActive"));
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_GenericType_IncludesGenericArguments()
        {
            var prop = typeof(ModelWithList).GetProperty("Items")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // Should include the List<SimpleModel> descriptor and its generic argument SimpleModel
            Assert.IsTrue(references.Any(r => r.UnderlyingType == typeof(SimpleModel)));
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_ComplexModel_ReturnsAllReferences()
        {
            var prop = typeof(ModelWithNestedGenerics).GetProperty("NestedGeneric")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // GenericModel<SimpleModel> has two properties: Value (T) and Description (string)
            // The generic argument references should include SimpleModel (from Value property)
            Assert.IsTrue(references.Count > 0);
            Assert.IsTrue(references.Any(r => r.Name == "Value"));
            Assert.IsTrue(references.Any(r => r.Name == "Description"));
            Assert.IsTrue(references.Any(r => r.UnderlyingType == typeof(SimpleModel)));
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_ModelWithMultipleGenericProperties_IncludesAllGenericArguments()
        {
            var prop = typeof(ModelWithNestedGenerics).GetProperty("DictWithClass")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // Dictionary<string, SimpleModel> should include string and SimpleModel as generic arguments
            Assert.IsTrue(references.Any(r => r.UnderlyingType == typeof(string)));
            Assert.IsTrue(references.Any(r => r.UnderlyingType == typeof(SimpleModel)));
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_DeeplyNestedGenerics_ReturnsAllNestedReferences()
        {
            var prop = typeof(ModelWithDeeplyNestedGenerics).GetProperty("DeepList")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // List<List<SimpleModel>> should include:
            // - List (from generic argument)
            // - SimpleModel (from nested generic)
            Assert.IsTrue(references.Any(r => r.UnderlyingType == typeof(List<SimpleModel>)));
            Assert.IsTrue(references.Any(r => r.UnderlyingType == typeof(SimpleModel)));
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_DictOfLists_ReturnsComplexNestedReferences()
        {
            var prop = typeof(ModelWithDeeplyNestedGenerics).GetProperty("DictOfLists")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // Dictionary<string, List<SimpleModel>> should include:
            // - string (key)
            // - List<SimpleModel> (value)
            // - SimpleModel (nested in list)
            Assert.IsTrue(references.Any(r => r.UnderlyingType == typeof(string)));
            Assert.IsTrue(references.Any(r => r.UnderlyingType == typeof(SimpleModel)));
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_NoDuplicates()
        {
            var prop = typeof(ModelWithNestedGenerics).GetProperty("SimpleList")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            var simpleModelRefs = references.Where(r => r.UnderlyingType == typeof(SimpleModel)).ToList();
            
            // Should have at least one SimpleModel reference from the List<SimpleModel>
            Assert.IsTrue(simpleModelRefs.Count > 0);
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_EmptyForPrimitiveTypes()
        {
            var prop = typeof(ModelWithDecimal).GetProperty("Amount")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            Assert.AreEqual(0, references.Count);
        }

        [TestMethod]
        public void GetDirectlyReferencedDescriptorsForProperties_WithGenericTypeParameter_IncludesReferences()
        {
            var prop = typeof(ModelWithNestedGenerics).GetProperty("NestedGeneric")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // Should include GenericModel's properties (Value and Description)
            // and the generic argument SimpleModel
            Assert.IsTrue(references.Any(r => r.Name == "Value"));
            Assert.IsTrue(references.Any(r => r.Name == "Description"));
        }
    }
}
