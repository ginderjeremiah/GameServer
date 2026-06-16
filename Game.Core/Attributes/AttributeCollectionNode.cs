using Game.Core.Attributes.Modifiers;
using Game.Core.Collections;

namespace Game.Core.Attributes
{
    internal class AttributeCollectionNode
    {
        // Orders modifiers by type. Built from a non-nullable Comparison delegate so the comparer needs
        // no null-forgiving operator on this mid-battle add/remove hot path, and shared as a single static
        // instance rather than allocated per node.
        private static readonly IComparer<AttributeModifier> TypeComparer =
            Comparer<AttributeModifier>.Create(static (x, y) => x.Type - y.Type);

        public SortedLinkedList<AttributeModifier> Modifiers { get; } = new(TypeComparer);
        public double? CachedValue { get; private set; }
        public HashSet<AttributeCollectionNode> DerivedNodes { get; } = [];

        public void SetCachedValue(double? value)
        {
            CachedValue = value;
            foreach (var derivedNode in DerivedNodes)
            {
                if (derivedNode.CachedValue is not null)
                {
                    derivedNode.SetCachedValue(null);
                }
            }
        }
    }
}
