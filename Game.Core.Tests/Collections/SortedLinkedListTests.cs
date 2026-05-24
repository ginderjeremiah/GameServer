using Game.Core.Collections;

namespace Game.Core.Tests.Collections
{
    [TestClass]
    public class SortedLinkedListTests
    {
        [TestMethod]
        public void Add_SingleElement_CountIsOne()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(5);

            Assert.AreEqual(1, list.Count);
        }

        [TestMethod]
        public void Add_MultipleElements_MaintainsSortedOrder()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(3);
            list.Add(1);
            list.Add(4);
            list.Add(1);
            list.Add(5);
            list.Add(2);

            var result = list.ToList();
            Assert.AreEqual(6, result.Count);
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 3, 4, 5 }, result);
        }

        [TestMethod]
        public void Add_AlreadySorted_MaintainsOrder()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, list.ToList());
        }

        [TestMethod]
        public void Add_ReverseSorted_SortsCorrectly()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(3);
            list.Add(2);
            list.Add(1);

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, list.ToList());
        }

        [TestMethod]
        public void Add_Duplicates_PreservesAll()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(2);
            list.Add(2);
            list.Add(2);

            Assert.AreEqual(3, list.Count);
            CollectionAssert.AreEqual(new[] { 2, 2, 2 }, list.ToList());
        }

        [TestMethod]
        public void Add_WithCustomComparer_SortsByComparer()
        {
            var list = new SortedLinkedList<string>((a, b) => a.Length.CompareTo(b.Length));
            list.Add("hello");
            list.Add("hi");
            list.Add("hey");
            list.Add("a");

            var result = list.ToList();
            Assert.AreEqual("a", result[0]);
            Assert.AreEqual("hi", result[1]);
            Assert.AreEqual("hey", result[2]);
            Assert.AreEqual("hello", result[3]);
        }

        [TestMethod]
        public void Add_StableForEqualElements_InsertsAfterExisting()
        {
            var list = new SortedLinkedList<(int Key, string Label)>(
                (a, b) => a.Key.CompareTo(b.Key));

            list.Add((1, "first"));
            list.Add((1, "second"));
            list.Add((1, "third"));

            var result = list.ToList();
            Assert.AreEqual("first", result[0].Label);
            Assert.AreEqual("second", result[1].Label);
            Assert.AreEqual("third", result[2].Label);
        }

        [TestMethod]
        public void Remove_ExistingElement_RemovesAndReturnsTrue()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var removed = list.Remove(2);

            Assert.IsTrue(removed);
            Assert.AreEqual(2, list.Count);
            CollectionAssert.AreEqual(new[] { 1, 3 }, list.ToList());
        }

        [TestMethod]
        public void Remove_Head_RemovesFirst()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var removed = list.Remove(1);

            Assert.IsTrue(removed);
            CollectionAssert.AreEqual(new[] { 2, 3 }, list.ToList());
        }

        [TestMethod]
        public void Remove_Tail_RemovesLast()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var removed = list.Remove(3);

            Assert.IsTrue(removed);
            CollectionAssert.AreEqual(new[] { 1, 2 }, list.ToList());
        }

        [TestMethod]
        public void Remove_NonExistentElement_ReturnsFalse()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(3);

            var removed = list.Remove(2);

            Assert.IsFalse(removed);
            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public void Remove_FromEmpty_ReturnsFalse()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);

            Assert.IsFalse(list.Remove(1));
        }

        [TestMethod]
        public void Remove_Duplicate_RemovesOnlyFirst()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(2);
            list.Add(2);
            list.Add(2);

            list.Remove(2);

            Assert.AreEqual(2, list.Count);
            CollectionAssert.AreEqual(new[] { 2, 2 }, list.ToList());
        }

        [TestMethod]
        public void Remove_OnlyElement_LeavesEmpty()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(5);

            list.Remove(5);

            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(0, list.ToList().Count);
        }

        [TestMethod]
        public void Clear_RemovesAllElements()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            list.Clear();

            Assert.AreEqual(0, list.Count);
            Assert.AreEqual(0, list.ToList().Count);
        }

        [TestMethod]
        public void Clear_ThenAdd_WorksCorrectly()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(5);
            list.Add(3);
            list.Clear();
            list.Add(1);
            list.Add(2);

            CollectionAssert.AreEqual(new[] { 1, 2 }, list.ToList());
        }

        [TestMethod]
        public void Enumeration_EmptyList_YieldsNothing()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);

            Assert.AreEqual(0, list.Count());
        }

        [TestMethod]
        public void Enumeration_Foreach_VisitsAllInOrder()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(5);
            list.Add(1);
            list.Add(3);

            var visited = new List<int>();
            foreach (var item in list)
            {
                visited.Add(item);
            }

            CollectionAssert.AreEqual(new[] { 1, 3, 5 }, visited);
        }

        [TestMethod]
        public void Enumeration_MultipleEnumerations_ProduceSameResult()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(3);
            list.Add(1);
            list.Add(2);

            var first = list.ToList();
            var second = list.ToList();

            CollectionAssert.AreEqual(first, second);
        }

        [TestMethod]
        public void Enumeration_LinqOperations_Work()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(10);
            list.Add(20);
            list.Add(30);

            Assert.AreEqual(60, list.Sum());
            Assert.AreEqual(10, list.First());
            Assert.AreEqual(30, list.Last());
            Assert.AreEqual(20, list.Where(x => x > 15).First());
        }

        [TestMethod]
        public void Count_EmptyList_IsZero()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            Assert.AreEqual(0, list.Count);
        }

        [TestMethod]
        public void Count_AfterAddAndRemove_IsAccurate()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);
            list.Remove(2);

            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public void ComparisonConstructor_Works()
        {
            var list = new SortedLinkedList<int>((a, b) => b.CompareTo(a));
            list.Add(1);
            list.Add(2);
            list.Add(3);

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, list.ToList());
        }

        [TestMethod]
        public void Add_LargeNumberOfElements_MaintainsOrder()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            var random = new Random(42);
            var values = Enumerable.Range(0, 100).Select(_ => random.Next(1000)).ToArray();

            foreach (var v in values)
                list.Add(v);

            var result = list.ToList();
            Assert.AreEqual(100, result.Count);
            for (int i = 1; i < result.Count; i++)
            {
                Assert.IsTrue(result[i - 1] <= result[i],
                    $"Order violated at index {i}: {result[i - 1]} > {result[i]}");
            }
        }
    }
}
