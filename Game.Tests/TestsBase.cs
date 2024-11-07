using System.Collections;

namespace GameTests
{
    public class TestsBase
    {
        public void AssertObjectPropertiesAreEqual(object? obj1, object? obj2)
        {
            var props = obj1?.GetType()?.GetProperties() ?? obj2?.GetType()?.GetProperties();

            if (props is null)
            {
                Assert.Inconclusive();
            }

            foreach (var prop in props)
            {
                var type = prop.PropertyType;
                Assert.IsNotNull(type);
                var value1 = prop.GetValue(obj1);
                var value2 = prop.GetValue(obj2);
                AssertValuesAreEqual(type, value1, value2);
            }
        }

        public void AssertEnumerablesAreEqual(IEnumerable enumerable1, IEnumerable enumerable2)
        {
            var enumerator2 = enumerable2.GetEnumerator();
            foreach (var item1 in enumerable1)
            {
                if (!enumerator2.MoveNext())
                {
                    Assert.Fail("Enumerables do not contain the same number of elements.");
                }
                var item2 = enumerator2.Current;
                if (item1 is not null || item2 is not null)
                {
                    var type1 = item1?.GetType();
                    var type2 = item2?.GetType();
                    Assert.AreEqual(type1, type2);
                    AssertValuesAreEqual(type1, item1, item2);
                }
            }

            if (enumerator2.MoveNext())
            {
                Assert.Fail("Enumerables do not contain the same number of elements.");
            }
        }

        private void AssertValuesAreEqual(Type type, object? value1, object? value2)
        {
            if (!type.IsClass || type == typeof(string))
            {
                Assert.AreEqual(value1, value2);
            }
            else if (type.IsAssignableTo(typeof(IEnumerable)))
            {
                AssertEnumerablesAreEqual((IEnumerable)value1!, (IEnumerable)value2!);
            }
            else
            {
                AssertObjectPropertiesAreEqual(value1, value2);
            }
        }
    }
}
