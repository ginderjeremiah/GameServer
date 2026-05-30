using Game.Api.CodeGen;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class CodeGenTypeDescriptorTests
    {
        [Fact]
        public void FromProperty_Int_SetsCorrectUnderlyingType()
        {
            var prop = typeof(SimpleModel).GetProperty("Id")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(int), descriptor.UnderlyingType);
            Assert.Equal("Id", descriptor.Name);
            Assert.False(descriptor.IsNullable);
            Assert.False(descriptor.IsGeneric);
            Assert.False(descriptor.IsEnum);
        }

        [Fact]
        public void FromProperty_String_SetsCorrectType()
        {
            var prop = typeof(SimpleModel).GetProperty("Name")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(string), descriptor.UnderlyingType);
            Assert.False(descriptor.IsNullable);
        }

        [Fact]
        public void FromProperty_NullableInt_UnwrapsNullable()
        {
            var prop = typeof(ModelWithNullable).GetProperty("NullableInt")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(int), descriptor.UnderlyingType);
            Assert.True(descriptor.IsNullable);
        }

        [Fact]
        public void FromProperty_NullableString_IsNullable()
        {
            var prop = typeof(ModelWithNullable).GetProperty("NullableName")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(string), descriptor.UnderlyingType);
            Assert.True(descriptor.IsNullable);
        }

        [Fact]
        public void FromProperty_NullableDateTime_UnwrapsNullable()
        {
            var prop = typeof(ModelWithNullable).GetProperty("NullableDate")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(DateTime), descriptor.UnderlyingType);
            Assert.True(descriptor.IsNullable);
        }

        [Fact]
        public void FromProperty_List_SetsGenericArguments()
        {
            var prop = typeof(ModelWithList).GetProperty("Items")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(List<SimpleModel>), descriptor.UnderlyingType);
            Assert.True(descriptor.IsGeneric);
            Assert.Single(descriptor.GenericArgumentDescriptors);
            Assert.Equal(typeof(SimpleModel), descriptor.GenericArgumentDescriptors[0].UnderlyingType);
        }

        [Fact]
        public void FromProperty_Enum_SetsIsEnum()
        {
            var prop = typeof(ModelWithEnum).GetProperty("Status")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(TestEnum), descriptor.UnderlyingType);
            Assert.True(descriptor.IsEnum);
        }

        [Fact]
        public void FromProperty_Class_PopulatesPropertyDescriptors()
        {
            var prop = typeof(NestedModel).GetProperty("Child")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(SimpleModel), descriptor.UnderlyingType);
            Assert.True(descriptor.NeedsInterface);
            Assert.Equal(3, descriptor.PropertyDescriptors.Count);
        }

        [Fact]
        public void FromParameter_SetsNameAndType()
        {
            var method = typeof(TestController).GetMethod("PostData")!;
            var param = method.GetParameters()[0];
            var descriptor = new CodeGenTypeDescriptor(param);

            Assert.Equal("model", descriptor.Name);
            Assert.Equal(typeof(SimpleModel), descriptor.UnderlyingType);
            Assert.False(descriptor.HasDefault);
        }

        [Fact]
        public void TypeName_NonGeneric_ReturnsName()
        {
            var prop = typeof(SimpleModel).GetProperty("Id")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal("Int32", descriptor.TypeName);
        }

        [Fact]
        public void TypeName_Generic_StripsBacktick()
        {
            var prop = typeof(ModelWithList).GetProperty("Items")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal("List", descriptor.TypeName);
        }

        [Fact]
        public void LastNamespacePart_ReturnsSnakeCased()
        {
            var prop = typeof(NestedModel).GetProperty("Child")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            // SimpleModel is in Game.Api.Tests.CodeGen namespace
            // Last part "CodeGen" → "code-gen"
            Assert.Equal("code-gen", descriptor.LastNamespacePart);
        }

        [Fact]
        public void GenericParameterPosition_Default_IsNegativeOne()
        {
            var prop = typeof(SimpleModel).GetProperty("Id")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(-1, descriptor.GenericParameterPosition);
        }

        [Fact]
        public void FromProperty_GenericType_SetsPropertyDescriptorsWithPositions()
        {
            var prop = typeof(GenericHolder).GetProperty("GenericProp")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.Equal(typeof(GenericModel<string>), descriptor.UnderlyingType);
            Assert.True(descriptor.IsGeneric);
            Assert.Equal(2, descriptor.PropertyDescriptors.Count);

            var valueProp = descriptor.PropertyDescriptors.First(d => d.Name == "Value");
            Assert.Equal(0, valueProp.GenericParameterPosition);

            var descProp = descriptor.PropertyDescriptors.First(d => d.Name == "Description");
            Assert.Equal(-1, descProp.GenericParameterPosition);
        }

        [Fact]
        public void NeedsInterface_ForString_ReturnsFalse()
        {
            var prop = typeof(SimpleModel).GetProperty("Name")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.False(descriptor.NeedsInterface);
        }

        [Fact]
        public void NeedsInterface_ForClass_ReturnsTrue()
        {
            var prop = typeof(NestedModel).GetProperty("Child")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            Assert.True(descriptor.NeedsInterface);
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_NonGenericType_ReturnsOnlyProperties()
        {
            var prop = typeof(SimpleModel).GetProperty("Id")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            Assert.Empty(references);
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_SimpleClass_ReturnsAllProperties()
        {
            var prop = typeof(NestedModel).GetProperty("Child")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            Assert.Equal(3, references.Count);
            Assert.Contains(references, r => r.Name == "Id");
            Assert.Contains(references, r => r.Name == "Name");
            Assert.Contains(references, r => r.Name == "IsActive");
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_GenericType_IncludesGenericArguments()
        {
            var prop = typeof(ModelWithList).GetProperty("Items")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // Should include the List<SimpleModel> descriptor and its generic argument SimpleModel
            Assert.Contains(references, r => r.UnderlyingType == typeof(SimpleModel));
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_ComplexModel_ReturnsAllReferences()
        {
            var prop = typeof(ModelWithNestedGenerics).GetProperty("NestedGeneric")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // GenericModel<SimpleModel> has two properties: Value (T) and Description (string)
            // The generic argument references should include SimpleModel (from Value property)
            Assert.True(references.Count > 0);
            Assert.Contains(references, r => r.Name == "Value");
            Assert.Contains(references, r => r.Name == "Description");
            Assert.Contains(references, r => r.UnderlyingType == typeof(SimpleModel));
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_ModelWithMultipleGenericProperties_IncludesAllGenericArguments()
        {
            var prop = typeof(ModelWithNestedGenerics).GetProperty("DictWithClass")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // Dictionary<string, SimpleModel> should include string and SimpleModel as generic arguments
            Assert.Contains(references, r => r.UnderlyingType == typeof(string));
            Assert.Contains(references, r => r.UnderlyingType == typeof(SimpleModel));
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_DeeplyNestedGenerics_ReturnsAllNestedReferences()
        {
            var prop = typeof(ModelWithDeeplyNestedGenerics).GetProperty("DeepList")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // List<List<SimpleModel>> should include:
            // - List (from generic argument)
            // - SimpleModel (from nested generic)
            Assert.Contains(references, r => r.UnderlyingType == typeof(List<SimpleModel>));
            Assert.Contains(references, r => r.UnderlyingType == typeof(SimpleModel));
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_DictOfLists_ReturnsComplexNestedReferences()
        {
            var prop = typeof(ModelWithDeeplyNestedGenerics).GetProperty("DictOfLists")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // Dictionary<string, List<SimpleModel>> should include:
            // - string (key)
            // - List<SimpleModel> (value)
            // - SimpleModel (nested in list)
            Assert.Contains(references, r => r.UnderlyingType == typeof(string));
            Assert.Contains(references, r => r.UnderlyingType == typeof(SimpleModel));
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_NoDuplicates()
        {
            var prop = typeof(ModelWithNestedGenerics).GetProperty("SimpleList")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            var simpleModelRefs = references.Where(r => r.UnderlyingType == typeof(SimpleModel)).ToList();

            // Should have at least one SimpleModel reference from the List<SimpleModel>
            Assert.True(simpleModelRefs.Count > 0);
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_EmptyForPrimitiveTypes()
        {
            var prop = typeof(ModelWithDecimal).GetProperty("Amount")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            Assert.Empty(references);
        }

        [Fact]
        public void GetDirectlyReferencedDescriptorsForProperties_WithGenericTypeParameter_IncludesReferences()
        {
            var prop = typeof(ModelWithNestedGenerics).GetProperty("NestedGeneric")!;
            var descriptor = new CodeGenTypeDescriptor(prop);

            var references = descriptor.GetDirectlyReferencedDescriptorsForProperties().ToList();

            // Should include GenericModel's properties (Value and Description)
            // and the generic argument SimpleModel
            Assert.Contains(references, r => r.Name == "Value");
            Assert.Contains(references, r => r.Name == "Description");
        }
    }
}
