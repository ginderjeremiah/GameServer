using Game.Api.CodeGen;
using Game.Api.Sockets.Commands;
using Xunit;

namespace Game.Api.Tests.CodeGen
{
    public class CodeGenExtensionsTests
    {
        [Fact]
        public void GetClosedGenericBase_DirectBase_ReturnsClosedType()
        {
            var result = typeof(TestSocketCommandFull).GetClosedGenericBase(typeof(AbstractSocketCommand<,>));

            Assert.NotNull(result);
            Assert.Equal(typeof(AbstractSocketCommand<SimpleModel, SocketParamModel>), result);
        }

        [Fact]
        public void GetClosedGenericBase_AncestorBase_WalksChain()
        {
            // AbstractSocketCommandWithResponseData<SimpleModel> is a grandparent of the full command.
            var result = typeof(TestSocketCommandFull).GetClosedGenericBase(typeof(AbstractSocketCommandWithResponseData<>));

            Assert.NotNull(result);
            Assert.Equal(typeof(AbstractSocketCommandWithResponseData<SimpleModel>), result);
        }

        [Fact]
        public void GetClosedGenericBase_NotDerived_ReturnsNull()
        {
            var result = typeof(TestSocketCommandBasic).GetClosedGenericBase(typeof(AbstractSocketCommandWithParams<>));

            Assert.Null(result);
        }

        [Fact]
        public void IsEnumerable_List_ReturnsTrue()
        {
            Assert.True(typeof(List<int>).IsEnumerable());
        }

        [Fact]
        public void IsEnumerable_IEnumerable_ReturnsTrue()
        {
            Assert.True(typeof(IEnumerable<string>).IsEnumerable());
        }

        [Fact]
        public void IsEnumerable_IAsyncEnumerable_ReturnsTrue()
        {
            Assert.True(typeof(IAsyncEnumerable<int>).IsEnumerable());
        }

        [Fact]
        public void IsEnumerable_Array_ReturnsFalse()
        {
            // Arrays are not generic types
            Assert.False(typeof(int[]).IsEnumerable());
        }

        [Fact]
        public void IsEnumerable_String_ReturnsFalse()
        {
            Assert.False(typeof(string).IsEnumerable());
        }

        [Fact]
        public void IsEnumerable_Int_ReturnsFalse()
        {
            Assert.False(typeof(int).IsEnumerable());
        }

        [Fact]
        public void IsEnumerable_Dictionary_ReturnsTrue()
        {
            Assert.True(typeof(Dictionary<string, int>).IsEnumerable());
        }

        [Fact]
        public void IsDictionary_Dictionary_ReturnsTrue()
        {
            Assert.True(typeof(Dictionary<string, int>).IsDictionary());
        }

        [Fact]
        public void IsDictionary_IDictionary_ReturnsTrue()
        {
            Assert.True(typeof(IDictionary<string, int>).IsDictionary());
        }

        [Fact]
        public void IsDictionary_IReadOnlyDictionary_ReturnsTrue()
        {
            Assert.True(typeof(IReadOnlyDictionary<string, int>).IsDictionary());
        }

        [Fact]
        public void IsDictionary_List_ReturnsFalse()
        {
            Assert.False(typeof(List<int>).IsDictionary());
        }

        [Fact]
        public void IsEnumerable_NonGenericClass_ReturnsFalse()
        {
            Assert.False(typeof(SimpleModel).IsEnumerable());
        }

        [Fact]
        public void NeedsInterface_Class_ReturnsTrue()
        {
            Assert.True(typeof(SimpleModel).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_String_ReturnsFalse()
        {
            Assert.False(typeof(string).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_Enum_ReturnsTrue()
        {
            Assert.True(typeof(TestEnum).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_DateTime_ReturnsFalse()
        {
            Assert.False(typeof(DateTime).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_Guid_ReturnsFalse()
        {
            // Guid is a non-primitive struct with no TypeScript mapping; it must not generate an interface.
            Assert.False(typeof(Guid).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_TimeSpan_ReturnsFalse()
        {
            Assert.False(typeof(TimeSpan).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_DateTimeOffset_ReturnsFalse()
        {
            Assert.False(typeof(DateTimeOffset).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_ListOfGuid_ReturnsFalse()
        {
            // The element type is the unmapped struct, so the enumerable must not generate an interface.
            Assert.False(typeof(List<Guid>).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_ListOfClass_ReturnsTrue()
        {
            // For enumerables, checks the generic argument
            Assert.True(typeof(List<SimpleModel>).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_ListOfString_ReturnsFalse()
        {
            Assert.False(typeof(List<string>).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_ListOfEnum_ReturnsTrue()
        {
            Assert.True(typeof(List<TestEnum>).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_Int_ReturnsFalse()
        {
            Assert.False(typeof(int).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_Bool_ReturnsFalse()
        {
            Assert.False(typeof(bool).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_Decimal_ReturnsFalse()
        {
            Assert.False(typeof(decimal).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_Float_ReturnsFalse()
        {
            Assert.False(typeof(float).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_Long_ReturnsFalse()
        {
            Assert.False(typeof(long).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_DictionaryWithClassValue_ReturnsTrue()
        {
            // For dictionaries, checks the value type argument
            Assert.True(typeof(Dictionary<string, SimpleModel>).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_DictionaryWithStringValue_ReturnsFalse()
        {
            Assert.False(typeof(Dictionary<string, string>).NeedsInterface());
        }

        [Fact]
        public void NeedsInterface_DictionaryWithIntValue_ReturnsFalse()
        {
            Assert.False(typeof(Dictionary<string, int>).NeedsInterface());
        }
    }
}
