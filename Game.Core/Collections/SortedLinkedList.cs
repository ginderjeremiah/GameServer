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

        /// <summary>
        /// Removes the first node matching <paramref name="value"/> per
        /// <paramref name="equalityComparer"/> rather than the ordering comparer. Use this to
        /// remove a specific instance when several entries can share the same sort key (the
        /// ordering comparer would otherwise remove whichever entry sorts equal first).
        /// </summary>
        public bool Remove(T value, IEqualityComparer<T> equalityComparer)
        {
            if (_head is null)
            {
                return false;
            }

            if (equalityComparer.Equals(_head.Value, value))
            {
                _head = _head.Next;
                Count--;
                return true;
            }

            var current = _head;
            while (current.Next is not null)
            {
                if (equalityComparer.Equals(current.Next.Value, value))
                {
                    current.Next = current.Next.Next;
                    Count--;
                    return true;
                }

                current = current.Next;
            }

            return false;
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

            // Undefined before the first MoveNext or after it returns false, matching the BCL
            // enumerator contract (e.g. List<T>.Enumerator) — the ! here is deliberate, not an
            // oversight, for the same reason the BCL's own enumerators use it.
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
