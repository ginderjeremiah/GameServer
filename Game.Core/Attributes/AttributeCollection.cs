using Game.Core;
using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    /// <summary>
    /// Represents an aggregated collection of <see cref="AttributeModifier"/> objects.
    /// </summary>
    public class AttributeCollection
    {
        private static readonly int _attributesMaxId = GetMaxAttribute();
        private readonly List<AttributeCollectionNode> _attributeNodeList = GetNodeList();

        /// <inheritdoc cref="GetAttributeValue(EAttribute)"/>
        public double this[EAttribute index] => GetAttributeValue(index);

        /// <summary>
        /// Creates a new aggregate from an <see cref="IEnumerable{T}"/> of type <see cref="AttributeModifier"/>.
        /// </summary>
        /// <param name="modifiers"></param>
        public AttributeCollection(IEnumerable<AttributeModifier> modifiers)
        {
            foreach (var modifier in modifiers)
            {
                AddModifierWithoutCacheInvalidation(modifier);
            }

            AddStaticModifiers();
        }

        /// <summary>
        /// Adds the <paramref name="modifier"/> to the store.
        /// </summary>
        /// <param name="modifier"></param>
        public void AddModifier(AttributeModifier modifier)
        {
            AddModifierWithoutCacheInvalidation(modifier);
            _attributeNodeList[(int)modifier.Attribute].SetCachedValue(null);
        }

        /// <summary>
        /// Gets the final value of the <see cref="EAttribute"/> based on each <see cref="AttributeModifier"/>.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public double GetAttributeValue(EAttribute attribute)
        {
            var node = _attributeNodeList[(int)attribute];
            if (node.CachedValue is not null)
            {
                return node.CachedValue.Value;
            }

            var amount = 0.0;
            var modifiers = node.Modifiers;
            if (modifiers is not null)
            {
                foreach (var modifier in modifiers.Values)
                {
                    amount = modifier.Apply(amount, this);
                }
            }

            node.SetCachedValue(amount);
            return amount;
        }

        /// <summary>
        /// Retrieves all the <see cref="AttributeModifier"/> instances contained in the collection.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<AttributeModifier> AllModifiers()
        {
            return _attributeNodeList.SelectNotNull(n => n.Modifiers?.Values).SelectMany(v => v);
        }

        private void AddModifierWithoutCacheInvalidation(AttributeModifier modifier)
        {
            var node = _attributeNodeList[(int)modifier.Attribute];
            node.Modifiers ??= [];
            node.Modifiers.Add(modifier.Type, modifier);

            if (modifier.Source is EAttributeModifierSource.Derived)
            {
                var sourceNode = _attributeNodeList[(int)modifier.DerivedSource];
                sourceNode.DerivedNodes ??= [];
                if (sourceNode.DerivedNodes.Contains(node))
                {
                    throw new AttributeCircularDerivedModifierException(modifier);
                }

                sourceNode.DerivedNodes.Add(node);
            }
        }

        private void AddStaticModifiers()
        {
            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.CooldownRecoveryAgility);
            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.CooldownRecoveryDexterity);

            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.DefenseBase);
            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.DefenseEndurance);
            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.DefenseAgility);

            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.DropBonusLuck);

            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.MaxHealthBase);
            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.MaxHealthEndurance);
            AddModifierWithoutCacheInvalidation(StaticAttributeModifiers.MaxHealthStrength);
        }

        private static List<AttributeCollectionNode> GetNodeList()
        {
            return Enumerable.Repeat(new AttributeCollectionNode(), _attributesMaxId + 1).ToList();
        }

        private static int GetMaxAttribute()
        {
            var attributeValues = Enum.GetValues<EAttribute>();
            return (int)attributeValues.Max();
        }
    }
}
