using Game.Api.CodeGen;

namespace Game.Api.Tests.CodeGen
{
    [TestClass]
    public class CodeGenExtensionsTests
    {
        [TestMethod]
        public void IsEnumerable_List_ReturnsTrue()
        {
            Assert.IsTrue(typeof(List<int>).IsEnumerable());
        }

        [TestMethod]
        public void IsEnumerable_IEnumerable_ReturnsTrue()
        {
            Assert.IsTrue(typeof(IEnumerable<string>).IsEnumerable());
        }

        [TestMethod]
        public void IsEnumerable_IAsyncEnumerable_ReturnsTrue()
        {
            Assert.IsTrue(typeof(IAsyncEnumerable<int>).IsEnumerable());
        }

        [TestMethod]
        public void IsEnumerable_Array_ReturnsFalse()
        {
            // Arrays are not generic types
            Assert.IsFalse(typeof(int[]).IsEnumerable());
        }

        [TestMethod]
        public void IsEnumerable_String_ReturnsFalse()
        {
            Assert.IsFalse(typeof(string).IsEnumerable());
        }

        [TestMethod]
        public void IsEnumerable_Int_ReturnsFalse()
        {
            Assert.IsFalse(typeof(int).IsEnumerable());
        }

        [TestMethod]
        public void IsEnumerable_Dictionary_ReturnsTrue()
        {
            Assert.IsTrue(typeof(Dictionary<string, int>).IsEnumerable());
        }

        [TestMethod]
        public void IsDictionary_Dictionary_ReturnsTrue()
        {
            Assert.IsTrue(typeof(Dictionary<string, int>).IsDictionary());
        }

        [TestMethod]
        public void IsDictionary_IDictionary_ReturnsTrue()
        {
            Assert.IsTrue(typeof(IDictionary<string, int>).IsDictionary());
        }

        [TestMethod]
        public void IsDictionary_IReadOnlyDictionary_ReturnsTrue()
        {
            Assert.IsTrue(typeof(IReadOnlyDictionary<string, int>).IsDictionary());
        }

        [TestMethod]
        public void IsDictionary_List_ReturnsFalse()
        {
            Assert.IsFalse(typeof(List<int>).IsDictionary());
        }

        [TestMethod]
        public void IsEnumerable_NonGenericClass_ReturnsFalse()
        {
            Assert.IsFalse(typeof(SimpleModel).IsEnumerable());
        }

        [TestMethod]
        public void NeedsInterface_Class_ReturnsTrue()
        {
            Assert.IsTrue(typeof(SimpleModel).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_String_ReturnsFalse()
        {
            Assert.IsFalse(typeof(string).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_Enum_ReturnsTrue()
        {
            Assert.IsTrue(typeof(TestEnum).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_DateTime_ReturnsFalse()
        {
            Assert.IsFalse(typeof(DateTime).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_ListOfClass_ReturnsTrue()
        {
            // For enumerables, checks the generic argument
            Assert.IsTrue(typeof(List<SimpleModel>).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_ListOfString_ReturnsFalse()
        {
            Assert.IsFalse(typeof(List<string>).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_ListOfEnum_ReturnsTrue()
        {
            Assert.IsTrue(typeof(List<TestEnum>).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_Int_ReturnsFalse()
        {
            Assert.IsFalse(typeof(int).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_Bool_ReturnsFalse()
        {
            Assert.IsFalse(typeof(bool).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_Decimal_ReturnsFalse()
        {
            Assert.IsFalse(typeof(decimal).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_Float_ReturnsFalse()
        {
            Assert.IsFalse(typeof(float).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_Long_ReturnsFalse()
        {
            Assert.IsFalse(typeof(long).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_DictionaryWithClassValue_ReturnsTrue()
        {
            // For dictionaries, checks the value type argument
            Assert.IsTrue(typeof(Dictionary<string, SimpleModel>).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_DictionaryWithStringValue_ReturnsFalse()
        {
            Assert.IsFalse(typeof(Dictionary<string, string>).NeedsInterface());
        }

        [TestMethod]
        public void NeedsInterface_DictionaryWithIntValue_ReturnsFalse()
        {
            Assert.IsFalse(typeof(Dictionary<string, int>).NeedsInterface());
        }
    }
}
