using Game.Core.Collections;
using Xunit;

namespace Game.Core.Tests.Collections
{
    public class SortedLinkedListTests
    {
        [Fact]
        public void Add_SingleElement_CountIsOne()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(5);

            Assert.Equal(1, list.Count);
        }

        [Fact]
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
            Assert.Equal(6, result.Count);
            Assert.Equal(new[] { 1, 1, 2, 3, 4, 5 }, result);
        }

        [Fact]
        public void Add_AlreadySorted_MaintainsOrder()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            Assert.Equal(new[] { 1, 2, 3 }, list.ToList());
        }

        [Fact]
        public void Add_ReverseSorted_SortsCorrectly()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(3);
            list.Add(2);
            list.Add(1);

            Assert.Equal(new[] { 1, 2, 3 }, list.ToList());
        }

        [Fact]
        public void Add_Duplicates_PreservesAll()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(2);
            list.Add(2);
            list.Add(2);

            Assert.Equal(3, list.Count);
            Assert.Equal(new[] { 2, 2, 2 }, list.ToList());
        }

        [Fact]
        public void Add_WithCustomComparer_SortsByComparer()
        {
            var list = new SortedLinkedList<string>((a, b) => a.Length.CompareTo(b.Length));
            list.Add("hello");
            list.Add("hi");
            list.Add("hey");
            list.Add("a");

            var result = list.ToList();
            Assert.Equal("a", result[0]);
            Assert.Equal("hi", result[1]);
            Assert.Equal("hey", result[2]);
            Assert.Equal("hello", result[3]);
        }

        [Fact]
        public void Add_StableForEqualElements_InsertsAfterExisting()
        {
            var list = new SortedLinkedList<(int Key, string Label)>(
                (a, b) => a.Key.CompareTo(b.Key));

            list.Add((1, "first"));
            list.Add((1, "second"));
            list.Add((1, "third"));

            var result = list.ToList();
            Assert.Equal("first", result[0].Label);
            Assert.Equal("second", result[1].Label);
            Assert.Equal("third", result[2].Label);
        }

        [Fact]
        public void Remove_ExistingElement_RemovesAndReturnsTrue()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var removed = list.Remove(2);

            Assert.True(removed);
            Assert.Equal(2, list.Count);
            Assert.Equal(new[] { 1, 3 }, list.ToList());
        }

        [Fact]
        public void Remove_Head_RemovesFirst()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var removed = list.Remove(1);

            Assert.True(removed);
            Assert.Equal(new[] { 2, 3 }, list.ToList());
        }

        [Fact]
        public void Remove_Tail_RemovesLast()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);

            var removed = list.Remove(3);

            Assert.True(removed);
            Assert.Equal(new[] { 1, 2 }, list.ToList());
        }

        [Fact]
        public void Remove_NonExistentElement_ReturnsFalse()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(3);

            var removed = list.Remove(2);

            Assert.False(removed);
            Assert.Equal(2, list.Count);
        }

        [Fact]
        public void Remove_FromEmpty_ReturnsFalse()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);

            Assert.False(list.Remove(1));
        }

        [Fact]
        public void Remove_Duplicate_RemovesOnlyFirst()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(2);
            list.Add(2);
            list.Add(2);

            list.Remove(2);

            Assert.Equal(2, list.Count);
            Assert.Equal(new[] { 2, 2 }, list.ToList());
        }

        [Fact]
        public void Remove_OnlyElement_LeavesEmpty()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(5);

            list.Remove(5);

            Assert.Equal(0, list.Count);
            Assert.Empty(list.ToList());
        }

        [Fact]
        public void Clear_RemovesAllElements()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default)
            {
                1,
                2,
                3
            };

            list.Clear();

            Assert.Equal(0, list.Count);
            Assert.Empty(list.ToList());
        }

        [Fact]
        public void Clear_ThenAdd_WorksCorrectly()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(5);
            list.Add(3);
            list.Clear();
            list.Add(1);
            list.Add(2);

            Assert.Equal(new[] { 1, 2 }, list.ToList());
        }

        [Fact]
        public void Enumeration_EmptyList_YieldsNothing()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);

            Assert.Empty(list);
        }

        [Fact]
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

            Assert.Equal(new[] { 1, 3, 5 }, visited);
        }

        [Fact]
        public void Enumeration_MultipleEnumerations_ProduceSameResult()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(3);
            list.Add(1);
            list.Add(2);

            var first = list.ToList();
            var second = list.ToList();

            Assert.Equal(first, second);
        }

        [Fact]
        public void Enumeration_LinqOperations_Work()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(10);
            list.Add(20);
            list.Add(30);

            Assert.Equal(60, list.Sum());
            Assert.Equal(10, list.First());
            Assert.Equal(30, list.Last());
            Assert.Equal(20, list.Where(x => x > 15).First());
        }

        [Fact]
        public void Count_EmptyList_IsZero()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void Count_AfterAddAndRemove_IsAccurate()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            list.Add(1);
            list.Add(2);
            list.Add(3);
            list.Remove(2);

            Assert.Equal(2, list.Count);
        }

        [Fact]
        public void ComparisonConstructor_Works()
        {
            var list = new SortedLinkedList<int>((a, b) => b.CompareTo(a));
            list.Add(1);
            list.Add(2);
            list.Add(3);

            Assert.Equal(new[] { 3, 2, 1 }, list.ToList());
        }

        [Fact]
        public void Add_LargeNumberOfElements_MaintainsOrder()
        {
            var list = new SortedLinkedList<int>(Comparer<int>.Default);
            var random = new Random(42);
            var values = Enumerable.Range(0, 100).Select(_ => random.Next(1000)).ToArray();

            foreach (var v in values)
            {
                list.Add(v);
            }

            var result = list.ToList();
            Assert.Equal(100, result.Count);
            for (int i = 1; i < result.Count; i++)
            {
                Assert.True(result[i - 1] <= result[i],
                    $"Order violated at index {i}: {result[i - 1]} > {result[i]}");
            }
        }

        // ── Remove(value, equalityComparer): identity-aware removal ──
        // The ordering comparer here sorts by Key only, so two distinct boxes can share a sort
        // key; the equality overload must remove the targeted instance, not whichever sorts equal.

        private sealed class Box(int key)
        {
            public int Key { get; } = key;
        }

        [Fact]
        public void RemoveWithEqualityComparer_RemovesTargetedInstanceAmongSortEqualEntries()
        {
            var list = new SortedLinkedList<Box>((a, b) => a.Key.CompareTo(b.Key));
            var first = new Box(1);
            var second = new Box(1);
            var third = new Box(1);
            list.Add(first);
            list.Add(second);
            list.Add(third);

            var removed = list.Remove(second, EqualityComparer<Box>.Default);

            Assert.True(removed);
            Assert.Equal(2, list.Count);
            Assert.Equal(new[] { first, third }, list.ToList());
        }

        [Fact]
        public void RemoveWithEqualityComparer_NotPresent_ReturnsFalse()
        {
            var list = new SortedLinkedList<Box>((a, b) => a.Key.CompareTo(b.Key));
            list.Add(new Box(1));

            var removed = list.Remove(new Box(1), EqualityComparer<Box>.Default);

            Assert.False(removed);
            Assert.Equal(1, list.Count);
        }
    }
}
