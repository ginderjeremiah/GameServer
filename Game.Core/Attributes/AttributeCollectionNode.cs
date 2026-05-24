using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    internal class AttributeCollectionNode
    {
        public List<AttributeModifier>? Modifiers { get; set; }
        public double? CachedValue { get; private set; }
        public List<AttributeCollectionNode>? DerivedNodes { get; set; }

        public void SetCachedValue(double? value)
        {
            CachedValue = value;
            if (DerivedNodes is not null)
            {
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
}
