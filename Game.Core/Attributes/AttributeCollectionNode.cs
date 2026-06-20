using Game.Core.Attributes.Modifiers;
using Game.Core.Collections;

namespace Game.Core.Attributes
{
    internal class AttributeCollectionNode
    {
        // Orders modifiers by type. Built from a non-nullable Comparison delegate so the comparer needs
        // no null-forgiving operator on this mid-battle add/remove hot path, and shared as a single static
        // instance rather than allocated per node. Enum subtraction is used as a micro optimization.
        // There is no need to worry about overflow here.
        private static readonly IComparer<AttributeModifier> TypeComparer =
            Comparer<AttributeModifier>.Create(static (x, y) => x.Type - y.Type);

        // Allocated lazily on the first stored modifier (see GetOrCreateModifiers). A node that only
        // ever holds a derived/cached value — an untouched derived-source attribute, or an empty slot
        // read through the indexer — never allocates the list, so on this hot path a null Modifiers
        // means "no stored modifiers". The read and remove paths treat null as the empty set.
        public SortedLinkedList<AttributeModifier>? Modifiers { get; private set; }
        public double? CachedValue { get; private set; }

        // Allocated lazily on the first derived link (see GetOrCreateDerivedNodes), mirroring Modifiers. A
        // node that nothing derives from — e.g. the player-only crit/dodge/block attributes an enemy battler
        // reads as zero on every DamageTarget call — never allocates the set. The cascade and unhook paths
        // treat null as the empty set.
        public HashSet<AttributeCollectionNode>? DerivedNodes { get; private set; }

        // Returns the modifier list, allocating it on first use. Only the add path calls this; the
        // read and remove paths leave the list null when no modifier was ever stored.
        public SortedLinkedList<AttributeModifier> GetOrCreateModifiers()
        {
            return Modifiers ??= new(TypeComparer);
        }

        // Returns the derived-node set, allocating it on first use. Only the derived-link add path calls
        // this; the cascade and unhook paths leave the set null when no link was ever created.
        public HashSet<AttributeCollectionNode> GetOrCreateDerivedNodes()
        {
            return DerivedNodes ??= [];
        }

        public void SetCachedValue(double? value)
        {
            CachedValue = value;
            if (DerivedNodes is null)
            {
                return;
            }

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
