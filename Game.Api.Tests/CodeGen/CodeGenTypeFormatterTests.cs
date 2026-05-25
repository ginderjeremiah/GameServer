using Game.Api.CodeGen;
using Game.Api.CodeGen.Data;

namespace Game.Api.Tests.CodeGen
{
    [TestClass]
    public class CodeGenTypeFormatterTests
    {
        private static CodeGenTypeDescriptor GetPropertyDescriptor<T>(string propertyName)
        {
            var property = typeof(T).GetProperty(propertyName)!;
            return new CodeGenTypeDescriptor(property);
        }

        [TestMethod]
        public void GetTypeText_Null_ReturnsUndefined()
        {
            Assert.AreEqual("undefined", CodeGenTypeFormatter.GetTypeText(null));
        }

        [TestMethod]
        public void GetTypeText_Int_ReturnsNumber()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Id");
            Assert.AreEqual("number", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_Decimal_ReturnsNumber()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDecimal>("Amount");
            Assert.AreEqual("number", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_Float_ReturnsNumber()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDecimal>("Rate");
            Assert.AreEqual("number", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_Bool_ReturnsBoolean()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("IsActive");
            Assert.AreEqual("boolean", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_String_ReturnsString()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Name");
            Assert.AreEqual("string", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_DateTime_ReturnsString()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDateTime>("CreatedAt");
            Assert.AreEqual("string", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_Enum_ReturnsEnumName()
        {
            var descriptor = GetPropertyDescriptor<ModelWithEnum>("Status");
            Assert.AreEqual("TestEnum", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_ListOfClass_ReturnsArrayType()
        {
            var descriptor = GetPropertyDescriptor<ModelWithList>("Items");
            Assert.AreEqual("ISimpleModel[]", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_ListOfInt_ReturnsNumberArray()
        {
            var descriptor = GetPropertyDescriptor<ModelWithList>("Numbers");
            Assert.AreEqual("number[]", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_Class_ReturnsInterfaceName()
        {
            var descriptor = GetPropertyDescriptor<NestedModel>("Child");
            Assert.AreEqual("ISimpleModel", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetImportText_Class_ReturnsPrefixedName()
        {
            var descriptor = GetPropertyDescriptor<NestedModel>("Child");
            Assert.AreEqual("ISimpleModel", CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [TestMethod]
        public void GetImportText_Enum_ReturnsEnumName()
        {
            var descriptor = GetPropertyDescriptor<ModelWithEnum>("Status");
            Assert.AreEqual("TestEnum", CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [TestMethod]
        public void GetImportText_String_ReturnsNull()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Name");
            Assert.IsNull(CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [TestMethod]
        public void GetImportText_ListOfClass_ReturnsElementImport()
        {
            var descriptor = GetPropertyDescriptor<ModelWithList>("Items");
            Assert.AreEqual("ISimpleModel", CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [TestMethod]
        public void GetImportText_Multiple_FormatsInlineForFewTypes()
        {
            var descriptors = new[]
            {
                GetPropertyDescriptor<NestedModel>("Child"),
                GetPropertyDescriptor<ModelWithEnum>("Status"),
            };

            var result = CodeGenTypeFormatter.GetImportText(descriptors);
            Assert.IsTrue(result.Contains("ISimpleModel"));
            Assert.IsTrue(result.Contains("TestEnum"));
            Assert.IsTrue(result.StartsWith("import type { "));
            Assert.IsTrue(result.Contains("from \"./\""));
        }

        [TestMethod]
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
            Assert.IsTrue(result.StartsWith("import type { "));
        }

        [TestMethod]
        public void GetImportText_CustomPath_UsesProvidedPath()
        {
            var descriptors = new[] { GetPropertyDescriptor<NestedModel>("Child") };
            var result = CodeGenTypeFormatter.GetImportText(descriptors, "../types/");
            Assert.IsTrue(result.Contains("from \"../types/\""));
        }

        [TestMethod]
        public void GetParameterText_SimpleProperty_FormatsCorrectly()
        {
            var descriptor = GetPropertyDescriptor<SimpleModel>("Id");
            var result = CodeGenTypeFormatter.GetParameterText(descriptor);
            Assert.AreEqual("id: number", result);
        }

        [TestMethod]
        public void GetParameterText_NullableProperty_AddsQuestionMark()
        {
            var descriptor = GetPropertyDescriptor<ModelWithNullable>("NullableInt");
            var result = CodeGenTypeFormatter.GetParameterText(descriptor);
            Assert.AreEqual("nullableInt?: number", result);
        }

        [TestMethod]
        public void GetParameterText_NullableString_AddsQuestionMark()
        {
            var descriptor = GetPropertyDescriptor<ModelWithNullable>("NullableName");
            var result = CodeGenTypeFormatter.GetParameterText(descriptor);
            Assert.AreEqual("nullableName?: string", result);
        }

        [TestMethod]
        public void GetInterfaceName_SimpleClass_ReturnsIPrefixed()
        {
            var descriptor = GetPropertyDescriptor<NestedModel>("Child");
            Assert.AreEqual("ISimpleModel", CodeGenTypeFormatter.GetInterfaceName(descriptor));
        }

        [TestMethod]
        public void GetInterfaceName_WithGenericParameters_IncludesTypeArgs()
        {
            var property = typeof(GenericHolder).GetProperty("GenericProp")!;
            var descriptor = new CodeGenTypeDescriptor(property);
            var result = CodeGenTypeFormatter.GetInterfaceName(descriptor);
            // GenericModel<string> → IGenericModel<string>
            Assert.AreEqual("IGenericModel<string>", result);
        }

        [TestMethod]
        public void GetInterfaceName_WithUseGenericParameters_UsesLetters()
        {
            var property = typeof(GenericHolder).GetProperty("GenericProp")!;
            var descriptor = new CodeGenTypeDescriptor(property);
            var result = CodeGenTypeFormatter.GetInterfaceName(descriptor, useGenericParameters: true);
            Assert.AreEqual("IGenericModel<T>", result);
        }

        [TestMethod]
        public void GetParametersTypeText_NoParameters_ReturnsVoid()
        {
            var method = typeof(TestController).GetMethod("GetSimple")!;
            var endpoint = new EndpointMetadata(method);
            Assert.AreEqual("void", CodeGenTypeFormatter.GetParametersTypeText(endpoint));
        }

        [TestMethod]
        public void GetParametersTypeText_SingleClassParam_ReturnsTypeName()
        {
            var method = typeof(TestController).GetMethod("PostData")!;
            var endpoint = new EndpointMetadata(method);
            var result = CodeGenTypeFormatter.GetParametersTypeText(endpoint);
            Assert.AreEqual("ISimpleModel", result);
        }

        [TestMethod]
        public void GetParametersTypeText_MultipleParams_ReturnsAnonymousObject()
        {
            var method = typeof(MultiParamController).GetMethod("UpdateMultiple")!;
            var endpoint = new EndpointMetadata(method);
            var result = CodeGenTypeFormatter.GetParametersTypeText(endpoint);
            Assert.AreEqual("{ id: number, name: string }", result);
        }

        [TestMethod]
        public void GetParametersTypeText_AllOptionalParams_AppendsUndefined()
        {
            var method = typeof(MultiParamController).GetMethod("OptionalParams")!;
            var endpoint = new EndpointMetadata(method);
            var result = CodeGenTypeFormatter.GetParametersTypeText(endpoint);
            // id is not nullable/default, so not all params are optional
            Assert.IsFalse(result.EndsWith("| undefined"));
        }

        [TestMethod]
        public void GetTypeText_DictionaryStringInt_ReturnsRecord()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToInt");
            Assert.AreEqual("Record<string, number>", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_DictionaryStringClass_ReturnsRecordWithInterface()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToClass");
            Assert.AreEqual("Record<string, ISimpleModel>", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_DictionaryIntString_ReturnsRecordWithNumberKey()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("IntToString");
            Assert.AreEqual("Record<number, string>", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetTypeText_DictionaryStringNullableClass_ReturnsRecordWithUndefined()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToNullableClass");
            Assert.AreEqual("Record<string, ISimpleModel | undefined>", CodeGenTypeFormatter.GetTypeText(descriptor));
        }

        [TestMethod]
        public void GetImportText_DictionaryWithClassValue_ReturnsValueImport()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToClass");
            Assert.AreEqual("ISimpleModel", CodeGenTypeFormatter.GetImportText(descriptor));
        }

        [TestMethod]
        public void GetImportText_DictionaryWithPrimitiveValue_ReturnsNull()
        {
            var descriptor = GetPropertyDescriptor<ModelWithDictionary>("StringToInt");
            Assert.IsNull(CodeGenTypeFormatter.GetImportText(descriptor));
        }
    }

    public class GenericHolder
    {
        public GenericModel<string> GenericProp { get; set; } = new();
    }
}
