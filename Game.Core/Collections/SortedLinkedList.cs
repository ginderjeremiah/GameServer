using System.Collections;

namespace Game.Core.Collections
{
    public class SortedLinkedList<T> : IEnumerable<T>
    {
        private readonly IComparer<T> _comparer;
        private Node? _head;

        public int Count { get; private set; }

        public SortedLinkedList(IComparer<T> comparer)
        {
            _comparer = comparer;
        }

        public SortedLinkedList(Comparison<T> comparison)
            : this(Comparer<T>.Create(comparison))
        {
        }

        public void Add(T value)
        {
            var newNode = new Node(value);

            if (_head is null || _comparer.Compare(value, _head.Value) < 0)
            {
                newNode.Next = _head;
                _head = newNode;
            }
            else
            {
                var current = _head;
                while (current.Next is not null && _comparer.Compare(value, current.Next.Value) >= 0)
                {
                    current = current.Next;
                }

                newNode.Next = current.Next;
                current.Next = newNode;
            }

            Count++;
        }

        public bool Remove(T value)
        {
            if (_head is null)
                return false;

            if (_comparer.Compare(_head.Value, value) == 0)
            {
                _head = _head.Next;
                Count--;
                return true;
            }

            var current = _head;
            while (current.Next is not null)
            {
                if (_comparer.Compare(current.Next.Value, value) == 0)
                {
                    current.Next = current.Next.Next;
                    Count--;
                    return true;
                }

                current = current.Next;
            }

            return false;
        }

        public void Clear()
        {
            _head = null;
            Count = 0;
        }

        public Enumerator GetEnumerator() => new(_head);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T>
        {
            private readonly Node? _head;
            private Node? _current;
            private bool _started;

            internal Enumerator(Node? head)
            {
                _head = head;
            }

            public readonly T Current => _current!.Value;

            readonly object IEnumerator.Current => Current!;

            public bool MoveNext()
            {
                if (!_started)
                {
                    _current = _head;
                    _started = true;
                }
                else
                {
                    _current = _current?.Next;
                }

                return _current is not null;
            }

            public void Reset()
            {
                _current = null;
                _started = false;
            }

            public readonly void Dispose() { }
        }

        internal sealed class Node(T value)
        {
            public T Value { get; } = value;
            public Node? Next { get; set; }
        }
    }
}
