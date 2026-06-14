using Game.Core.Attributes.Modifiers;

namespace Game.Core.Attributes
{
    /// <summary>
    /// Represents an aggregated collection of <see cref="AttributeModifier"/> objects.
    /// </summary>
    public class AttributeCollection
    {
        private static readonly int _attributesMaxId = GetMaxAttribute();

        // One slot per attribute id; nodes are created lazily on first access so a freshly
        // constructed collection allocates no per-attribute nodes for the (common) empty slots.
        private readonly AttributeCollectionNode?[] _attributeNodes = new AttributeCollectionNode?[_attributesMaxId + 1];

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
            GetOrCreateNode((int)modifier.Attribute).SetCachedValue(null);
        }

        /// <summary>
        /// Removes the <paramref name="modifier"/> instance from the store, invalidating the
        /// affected node's cached value with the same cascading derived-node invalidation
        /// <see cref="AddModifier"/> performs. Returns whether the modifier was present.
        /// </summary>
        /// <param name="modifier"></param>
        public bool RemoveModifier(AttributeModifier modifier)
        {
            // A null slot (never created) holds no modifiers, so there is nothing to remove.
            var node = _attributeNodes[(int)modifier.Attribute];

            // Identity removal: a node can hold several modifiers with the same Type (the
            // collection's sort key), so the exact instance must be matched, not a sort-equal one.
            // AttributeModifier does not override Equals, so the default comparer is reference equality.
            if (node?.Modifiers is null || !node.Modifiers.Remove(modifier, EqualityComparer<AttributeModifier>.Default))
            {
                return false;
            }

            if (modifier.Source is EAttributeModifierSource.Derived)
            {
                UnhookDerivedLink(node, modifier.DerivedSource);
            }

            node.SetCachedValue(null);
            return true;
        }

        /// <summary>
        /// Gets the final value of the <see cref="EAttribute"/> based on each <see cref="AttributeModifier"/>.
        /// </summary>
        /// <param name="attribute"></param>
        /// <returns></returns>
        public double GetAttributeValue(EAttribute attribute)
        {
            // Create the node lazily so the computed value can be cached, matching the
            // pre-laziness behavior where every read populates the cache.
            var node = GetOrCreateNode((int)attribute);
            if (node.CachedValue is not null)
            {
                return node.CachedValue.Value;
            }

            var amount = 0.0;
            var modifiers = node.Modifiers;
            if (modifiers is not null)
            {
                foreach (var modifier in modifiers)
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
            return _attributeNodes.SelectNotNull(n => n?.Modifiers).SelectMany(v => v);
        }

        private void AddModifierWithoutCacheInvalidation(AttributeModifier modifier)
        {
            var node = GetOrCreateNode((int)modifier.Attribute);

            node.GetModifiersOrNew().Add(modifier);

            if (modifier.Source is EAttributeModifierSource.Derived)
            {
                var sourceNode = GetOrCreateNode((int)modifier.DerivedSource);
                if (node.DerivedNodes.Contains(sourceNode))
                {
                    throw new AttributeCircularDerivedModifierException(modifier);
                }

                sourceNode.DerivedNodes.Add(node);
            }
        }

        /// <summary>
        /// Unhooks the cascade link from <paramref name="derivedSource"/> to <paramref name="node"/>,
        /// but only when no remaining modifier on the node still derives from that source — so the
        /// source's cache-invalidation cascade stays correct rather than dropping a still-needed link.
        /// </summary>
        private void UnhookDerivedLink(AttributeCollectionNode node, EAttribute derivedSource)
        {
            foreach (var modifier in node.GetModifiersOrNew())
            {
                if (modifier.Source is EAttributeModifierSource.Derived && modifier.DerivedSource == derivedSource)
                {
                    return;
                }
            }

            // The source node exists because removing a Derived modifier implies it was hooked here.
            GetOrCreateNode((int)derivedSource).DerivedNodes.Remove(node);
        }

        private void AddStaticModifiers()
        {
            foreach (var modifier in StaticAttributeModifiers.All)
            {
                AddModifierWithoutCacheInvalidation(modifier);
            }
        }

        // Returns the node for the slot, creating its own instance on first access. Each slot
        // gets a distinct node — they must never share one, since they hold per-attribute state.
        private AttributeCollectionNode GetOrCreateNode(int index)
        {
            return _attributeNodes[index] ??= new AttributeCollectionNode();
        }

        private static int GetMaxAttribute()
        {
            var attributeValues = Enum.GetValues<EAttribute>();
            return (int)attributeValues.Max();
        }
    }
}
