using Game.Core.Attributes.Modifiers;
using Game.Core.Collections;

namespace Game.Core.Attributes
{
    internal class AttributeCollectionNode
    {
        private HashSet<AttributeCollectionNode>? _derivedNodes;

        public SortedLinkedList<AttributeModifier>? Modifiers { get; private set; }
        public double? CachedValue { get; private set; }
        public HashSet<AttributeCollectionNode> DerivedNodes => _derivedNodes ??= [];

        public void SetCachedValue(double? value)
        {
            CachedValue = value;
            if (_derivedNodes is not null)
            {
                foreach (var derivedNode in _derivedNodes)
                {
                    if (derivedNode.CachedValue is not null)
                    {
                        derivedNode.SetCachedValue(null);
                    }
                }
            }
        }

        public SortedLinkedList<AttributeModifier> GetModifiersOrNew()
        {
            return Modifiers ??= new SortedLinkedList<AttributeModifier>(new AttributeModifierNullUnsafeTypeComparer());
        }
    }
}
