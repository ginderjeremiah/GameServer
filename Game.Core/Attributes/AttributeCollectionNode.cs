using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    internal class AttributeCollectionNode
    {
        public List<AttributeModifier>? Modifiers { get; set; }
        public double? CachedValue { get; private set; }
        public List<AttributeCollectionNode> DerivedNodes { get; } = [];

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
