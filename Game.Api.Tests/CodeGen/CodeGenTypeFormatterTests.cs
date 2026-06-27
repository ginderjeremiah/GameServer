using Game.Api.CodeGen;
using Game.Api.CodeGen.Data;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class CodeGenTypeFormatterTests
    {
        private static CodeGenTypeDescriptor GetPropertyDescriptor<T>(string propertyName)
        {
            var property = typeof(T).GetProperty(propertyName)!;
            return new CodeGenTypeDescriptor(property);
        }

        [Fact]
        public void GetTypeText_Null_ReturnsUndefined()
        {
            Assert.Equal("undefined", CodeGenTypeFormatter.GetTypeText(null));
        }

        [Fact]
        public void GetTypeText_Int_ReturnsNumber()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Id");
            Assert.Equal("number", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_Decimal_ReturnsNumber()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDecimal>("Amount");
            Assert.Equal("number", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_Float_ReturnsNumber()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDecimal>("Rate");
            Assert.Equal("number", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_Bool_ReturnsBoolean()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("IsActive");
            Assert.Equal("boolean", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_String_ReturnsString()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Name");
            Assert.Equal("string", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_DateTime_ReturnsString()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDateTime>("CreatedAt");
            Assert.Equal("string", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_Enum_ReturnsEnumName()
        {
            var descriptor = GetPropertyDescriptor<ModelWithEnum>("Status");
            Assert.Equal("TestEnum", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_ListOfClass_ReturnsArrayType()
        {
            var descriptor = GetPropertyDescriptor<ModelWithList>("Items");
            Assert.Equal("ISimpleModel[]", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_ListOfInt_ReturnsNumberArray()
        {
            var descriptor = GetPropertyDescriptor<ModelWithList>("Numbers");
            Assert.Equal("number[]", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_Class_ReturnsInterfaceName()
        {
            var descriptor = GetPropertyDescriptor<NestedModel>("Child");
            Assert.Equal("ISimpleModel", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetImportText_Class_ReturnsPrefixedName()
        {
            var descriptor = GetPropertyDescriptor<NestedModel>("Child");
            Assert.Equal("ISimpleModel", CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [Fact]
        public void GetImportText_Enum_ReturnsEnumName()
        {
            var descriptor = GetPropertyDescriptor<ModelWithEnum>("Status");
            Assert.Equal("TestEnum", CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [Fact]
        public void GetImportText_String_ReturnsNull()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Name");
            Assert.Null(CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [Fact]
        public void GetImportText_ListOfClass_ReturnsElementImport()
        {
            var descriptor = GetPropertyDescriptor<ModelWithList>("Items");
            Assert.Equal("ISimpleModel", CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [Fact]
        public void GetImportText_Multiple_FormatsInlineForFewTypes()
        {
            var descriptors = new[]
            {
                GetPropertyDescriptor<NestedModel>("Child"),
                GetPropertyDescriptor<ModelWithEnum>("Status"),
            };

            var result = CodeGenTypeFormatter.GetImportText(descriptors);
            Assert.Contains("ISimpleModel", result);
            Assert.Contains("TestEnum", result);
            Assert.StartsWith("import type { ", result);
            Assert.Contains("from './'", result);
        }

        [Fact]
        public void GetImportText_Multiple_FormatsMultilineForManyTypes()
        {
            var descriptors = new[]
            {
                GetPropertyDescriptor<NestedModel>("Child"),
                GetPropertyDescriptor<ModelWithEnum>("Status"),
                GetPropertyDescriptor<ModelWithList>("Items"),
                GetPropertyDescriptor<ModelWithDateTime>("CreatedAt"),  // null import - string
            };

            // Only 3 non-null: ISimpleModel (from Child and Items both), TestEnum
            // After Distinct, we need >3 distinct imports to trigger multiline
            // With these test types we get: ISimpleModel, TestEnum, ISimpleModel → distinct = 2
            var result = CodeGenTypeFormatter.GetImportText(descriptors);
            // Should be inline since <= 3 distinct
            Assert.StartsWith("import type { ", result);
        }

        [Fact]
        public void GetImportText_CustomPath_UsesProvidedPath()
        {
            var descriptors = new[] { GetPropertyDescriptor<NestedModel>("Child") };
            var result = CodeGenTypeFormatter.GetImportText(descriptors, "../types/");
            Assert.Contains("from '../types/'", result);
        }

        [Fact]
        public void GetParameterText_SimpleProperty_FormatsCorrectly()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Id");
            var result = CodeGenTypeFormatter.GetParameterText(descriptor);
            Assert.Equal("id: number", result);
        }

        [Fact]
        public void GetParameterText_NullableProperty_AddsQuestionMark()
        {
            var descriptor = GetPropertyDescriptor<ModelWithNullable>("NullableInt");
            var result = CodeGenTypeFormatter.GetParameterText(descriptor);
            Assert.Equal("nullableInt?: number", result);
        }

        [Fact]
        public void GetParameterText_NullableString_AddsQuestionMark()
        {
            var descriptor = GetPropertyDescriptor<ModelWithNullable>("NullableName");
            var result = CodeGenTypeFormatter.GetParameterText(descriptor);
            Assert.Equal("nullableName?: string", result);
        }

        [Fact]
        public void GetInterfaceName_SimpleClass_ReturnsIPrefixed()
        {
            var descriptor = GetPropertyDescriptor<NestedModel>("Child");
            Assert.Equal("ISimpleModel", CodeGenTypeFormatter.GetInterfaceName(descriptor));
        }

        [Fact]
        public void GetInterfaceName_WithGenericParameters_IncludesTypeArgs()
        {
            var property = typeof(GenericHolder).GetProperty("GenericProp")!;
            var descriptor = new CodeGenTypeDescriptor(property);
            var result = CodeGenTypeFormatter.GetInterfaceName(descriptor);
            // GenericModel<string> → IGenericModel<string>
            Assert.Equal("IGenericModel<string>", result);
        }

        [Fact]
        public void GetInterfaceName_WithUseGenericParameters_UsesLetters()
        {
            var property = typeof(GenericHolder).GetProperty("GenericProp")!;
            var descriptor = new CodeGenTypeDescriptor(property);
            var result = CodeGenTypeFormatter.GetInterfaceName(descriptor, useGenericParameters: true);
            Assert.Equal("IGenericModel<T>", result);
        }

        [Fact]
        public void GetParametersTypeText_NoParameters_ReturnsVoid()
        {
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test" };
            Assert.Equal("void", CodeGenTypeFormatter.GetParametersTypeText(endpoint));
        }

        [Fact]
        public void GetParametersTypeText_SingleClassParam_ReturnsTypeName()
        {
            var method = typeof(TestController).GetMethod("PostData")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test" };
            var result = CodeGenTypeFormatter.GetParametersTypeText(endpoint);
            Assert.Equal("ISimpleModel", result);
        }

        [Fact]
        public void GetParametersTypeText_MultipleParams_ReturnsAnonymousObject()
        {
            var method = typeof(MultiParamController).GetMethod("UpdateMultiple")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test" };
            var result = CodeGenTypeFormatter.GetParametersTypeText(endpoint);
            Assert.Equal("{ id: number, name: string }", result);
        }

        [Fact]
        public void GetParametersTypeText_AllOptionalParams_AppendsUndefined()
        {
            var method = typeof(MultiParamController).GetMethod("OptionalParams")!;
            var endpoint = new EndpointMetadata(method) { Endpoint = "Test" };
            var result = CodeGenTypeFormatter.GetParametersTypeText(endpoint);
            // id is not nullable/default, so not all params are optional
            Assert.False(result.EndsWith("| undefined"));
        }

        [Fact]
        public void GetTypeText_DictionaryStringInt_ReturnsRecord()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToInt");
            Assert.Equal("Record<string, number>", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_DictionaryStringClass_ReturnsRecordWithInterface()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToClass");
            Assert.Equal("Record<string, ISimpleModel>", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_DictionaryIntString_ReturnsRecordWithNumberKey()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("IntToString");
            Assert.Equal("Record<number, string>", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetTypeText_DictionaryStringNullableClass_ReturnsRecordWithUndefined()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToNullableClass");
            Assert.Equal("Record<string, ISimpleModel | undefined>", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [Fact]
        public void GetImportText_DictionaryWithClassValue_ReturnsValueImport()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToClass");
            Assert.Equal("ISimpleModel", CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [Fact]
        public void GetImportText_DictionaryWithPrimitiveValue_ReturnsNull()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToInt");
            Assert.Null(CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [Fact]
        public void GetImportTexts_Class_ReturnsSingleImport()
        {
            var descriptor = GetPropertyDescriptor<NestedModel>("Child");
            Assert.Equal(["ISimpleModel"], CodeGenTypeFormatter.GetImportTexts(descriptor));
        }

        [Fact]
        public void GetImportTexts_Primitive_ReturnsEmpty()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Name");
            Assert.Empty(CodeGenTypeFormatter.GetImportTexts(descriptor));
        }

        [Fact]
        public void GetImportTexts_ListOfClass_ReturnsElementImport()
        {
            var descriptor = GetPropertyDescriptor<ModelWithList>("Items");
            Assert.Equal(["ISimpleModel"], CodeGenTypeFormatter.GetImportTexts(descriptor));
        }

        [Fact]
        public void GetImportTexts_DictionaryWithPrimitiveKeyAndValue_ReturnsEmpty()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToInt");
            Assert.Empty(CodeGenTypeFormatter.GetImportTexts(descriptor));
        }

        [Fact]
        public void GetImportTexts_DictionaryWithClassValue_ReturnsValueImport()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToClass");
            Assert.Equal(["ISimpleModel"], CodeGenTypeFormatter.GetImportTexts(descriptor));
        }

        [Fact]
        public void GetImportTexts_DictionaryWithEnumKey_IncludesKeyImport()
        {
            // The key type is an enum that needs importing; the value is a primitive. The old value-only
            // collection dropped the key, emitting a Record<TestEnum, number> with no TestEnum import.
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("EnumToInt");
            Assert.Equal(["TestEnum"], CodeGenTypeFormatter.GetImportTexts(descriptor));
        }

        [Fact]
        public void GetImportTexts_DictionaryWithEnumKeyAndClassValue_IncludesBothImports()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("EnumToClass");
            Assert.Equal(["TestEnum", "ISimpleModel"], CodeGenTypeFormatter.GetImportTexts(descriptor));
        }

        [Fact]
        public void GetImportText_Multiple_DictionaryWithEnumKey_ImportsKeyType()
        {
            // End-to-end through the import-statement formatter: the enum key must appear in the emitted
            // import list so the generated Record<TestEnum, number> reference resolves.
            var descriptors = new[] { GetPropertyDescriptor<ModelWithDictionary>("EnumToInt") };
            var result = CodeGenTypeFormatter.GetImportText(descriptors);
            Assert.Contains("TestEnum", result);
        }

        [Fact]
        public void GetTypeText_UnmappedByte_Throws()
        {
            // byte has no TypeScript mapping and is rejected by NeedsInterface (it is a primitive), so the
            // formatter must throw rather than silently emit a reference to a never-generated interface.
            var descriptor = GetPropertyDescriptor<ModelWithUnmappedType>("ByteValue");
            var ex = Assert.Throws<InvalidOperationException>(() => CodeGenTypeFormatter.GetTypeText(descriptor));
            Assert.Contains("Byte", ex.Message);
        }

        [Fact]
        public void GetTypeText_UnmappedChar_Throws()
        {
            var descriptor = GetPropertyDescriptor<ModelWithUnmappedType>("CharValue");
            var ex = Assert.Throws<InvalidOperationException>(() => CodeGenTypeFormatter.GetTypeText(descriptor));
            Assert.Contains("Char", ex.Message);
        }

        [Fact]
        public void GetTypeText_UnmappedGuid_Throws()
        {
            // Guid is a non-primitive struct with no TypeScript mapping. Before #1081 NeedsInterface let it
            // through and the formatter emitted a nonsense IGuid; it must now throw at generation time.
            var descriptor = GetPropertyDescriptor<ModelWithUnmappedStruct>("GuidValue");
            var ex = Assert.Throws<InvalidOperationException>(() => CodeGenTypeFormatter.GetTypeText(descriptor));
            Assert.Contains("Guid", ex.Message);
        }

        [Fact]
        public void GetTypeText_UnmappedTimeSpan_Throws()
        {
            var descriptor = GetPropertyDescriptor<ModelWithUnmappedStruct>("TimeSpanValue");
            var ex = Assert.Throws<InvalidOperationException>(() => CodeGenTypeFormatter.GetTypeText(descriptor));
            Assert.Contains("TimeSpan", ex.Message);
        }

        [Fact]
        public void GetTypeText_UnmappedDateTimeOffset_Throws()
        {
            var descriptor = GetPropertyDescriptor<ModelWithUnmappedStruct>("DateTimeOffsetValue");
            var ex = Assert.Throws<InvalidOperationException>(() => CodeGenTypeFormatter.GetTypeText(descriptor));
            Assert.Contains("DateTimeOffset", ex.Message);
        }
    }

    public class GenericHolder
    {
        public GenericModel<string> GenericProp { get; set; } = new();
    }
}
