using Game.Core.Attributes.Modifiers;
using Game.Core.Collections;

namespace Game.Core.Attributes
{
    internal class AttributeCollectionNode
    {
        public SortedLinkedList<AttributeModifier> Modifiers { get; } = new(new AttributeModifierNullUnsafeTypeComparer());
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
