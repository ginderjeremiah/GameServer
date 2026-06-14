using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Xunit;

namespace Game.Core.Tests.Attributes
{
    public class AttributeCollectionTests
    {
        [Fact]
        public void Indexer_AdditiveModifiers_SumsCorrectly()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Strength, 10),
                Additive(EAttribute.Strength, 5),
            };

            var collection = new AttributeCollection(modifiers);

            Assert.Equal(15, collection[EAttribute.Strength]);
        }

        [Fact]
        public void Indexer_NoModifiers_ReturnsZero()
        {
            var collection = new AttributeCollection([]);

            Assert.Equal(0, collection[EAttribute.Intellect]);
        }

        [Fact]
        public void Indexer_DerivedModifier_MultipliesBaseAttribute()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Endurance, 10),
            };

            var collection = new AttributeCollection(modifiers);

            // MaxHealth has static derived modifiers: base 50 + Endurance*20 + Strength*5
            // With Endurance=10, Strength=0: MaxHealth = 50 + 10*20 + 0*5 = 250
            Assert.Equal(250, collection[EAttribute.MaxHealth]);
        }

        [Fact]
        public void Indexer_MultipleBaseStats_DerivedComputesFromAll()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Endurance, 10),
                Additive(EAttribute.Strength, 5),
            };

            var collection = new AttributeCollection(modifiers);

            // MaxHealth = base(50) + Endurance(10)*20 + Strength(5)*5 = 50 + 200 + 25 = 275
            Assert.Equal(275, collection[EAttribute.MaxHealth]);
        }

        [Fact]
        public void Indexer_Defense_IncludesBaseAndDerived()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Endurance, 10),
                Additive(EAttribute.Agility, 4),
            };

            var collection = new AttributeCollection(modifiers);

            // Defense = base(2) + Endurance(10)*1.0 + Agility(4)*0.5 = 2 + 10 + 2 = 14
            Assert.Equal(14, collection[EAttribute.Defense]);
        }

        [Fact]
        public void Indexer_CooldownRecovery_DerivedFromAgilityAndDexterity()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Agility, 10),
                Additive(EAttribute.Dexterity, 20),
            };

            var collection = new AttributeCollection(modifiers);

            // CooldownRecovery = Agility(10)*0.4 + Dexterity(20)*0.1 = 4 + 2 = 6
            Assert.Equal(6, collection[EAttribute.CooldownRecovery]);
        }

        [Fact]
        public void Constructor_DuplicateDerivedModifierSameSourceTarget_Throws()
        {
            // MaxHealth has a static derived modifier from Strength, so adding a modifier to derive Strength from MaxHealth creates a circular dependency
            var modifiers = new List<AttributeModifier>
            {
                new()
                {
                    Attribute = EAttribute.Strength,
                    Amount = 1.0,
                    Type = EModifierType.Additive,
                    Source = EAttributeModifierSource.Derived,
                    DerivedSource = EAttribute.MaxHealth,
                },
            };

            Assert.Throws<AttributeCircularDerivedModifierException>(
                () => new AttributeCollection(modifiers));
        }

        [Fact]
        public void AddModifier_InvalidatesCachedValue()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Strength, 10),
            };
            var collection = new AttributeCollection(modifiers);

            var before = collection[EAttribute.Strength];
            collection.AddModifier(Additive(EAttribute.Strength, 5));
            var after = collection[EAttribute.Strength];

            Assert.Equal(10, before);
            Assert.Equal(15, after);
        }

        [Fact]
        public void RemoveModifier_RevertsTheValue()
        {
            var added = Additive(EAttribute.Strength, 5);
            var collection = new AttributeCollection([Additive(EAttribute.Strength, 10)]);
            collection.AddModifier(added);

            var before = collection[EAttribute.Strength];
            var removed = collection.RemoveModifier(added);
            var after = collection[EAttribute.Strength];

            Assert.True(removed);
            Assert.Equal(15, before);
            Assert.Equal(10, after);
        }

        [Fact]
        public void RemoveModifier_CascadesInvalidationToDerivedAttributes()
        {
            var strength = Additive(EAttribute.Strength, 10);
            var collection = new AttributeCollection([strength]);

            // MaxHealth derives from Strength (×5): 50 + 5*10 = 100 while the modifier is present.
            var before = collection[EAttribute.MaxHealth];
            collection.RemoveModifier(strength);
            var after = collection[EAttribute.MaxHealth];

            Assert.Equal(100, before);
            Assert.Equal(50, after);
        }

        [Fact]
        public void RemoveModifier_ThenReAdd_RestoresTheValue()
        {
            var modifier = Additive(EAttribute.Strength, 7);
            var collection = new AttributeCollection([modifier]);

            collection.RemoveModifier(modifier);
            Assert.Equal(0, collection[EAttribute.Strength]);

            collection.AddModifier(modifier);
            Assert.Equal(7, collection[EAttribute.Strength]);
        }

        [Fact]
        public void RemoveModifier_RemovesTheExactInstanceAmongSameTypeModifiers()
        {
            // Two additive Strength modifiers share a sort key (Type); only the targeted
            // instance must be removed, not whichever sorts equal first.
            var first = Additive(EAttribute.Strength, 10);
            var second = Additive(EAttribute.Strength, 3);
            var collection = new AttributeCollection([first, second]);

            collection.RemoveModifier(second);

            Assert.Equal(10, collection[EAttribute.Strength]);
        }

        [Fact]
        public void RemoveModifier_RemovesDerivedModifierAndUpdatesValue()
        {
            // Endurance contributes to MaxHealth (×20) both via the static derived modifier and
            // this extra one; removing the extra leaves the static contribution intact.
            var extraEndurance = new AttributeModifier
            {
                Attribute = EAttribute.MaxHealth,
                Amount = 3.0,
                Type = EModifierType.Additive,
                Source = EAttributeModifierSource.Derived,
                DerivedSource = EAttribute.Endurance,
            };
            var collection = new AttributeCollection([Additive(EAttribute.Endurance, 10)]);
            collection.AddModifier(extraEndurance);

            // 50 + (20 + 3)*10 = 280 with the extra derived modifier present.
            Assert.Equal(280, collection[EAttribute.MaxHealth]);

            collection.RemoveModifier(extraEndurance);

            // Back to the static-only derivation: 50 + 20*10 = 250.
            Assert.Equal(250, collection[EAttribute.MaxHealth]);

            // The cascade link from Endurance still works for the remaining static modifier.
            collection.AddModifier(Additive(EAttribute.Endurance, 5));
            Assert.Equal(50 + 20 * 15, collection[EAttribute.MaxHealth]);
        }

        [Fact]
        public void RemoveModifier_NotPresent_ReturnsFalse()
        {
            var collection = new AttributeCollection([Additive(EAttribute.Strength, 10)]);

            var removed = collection.RemoveModifier(Additive(EAttribute.Strength, 10));

            Assert.False(removed);
            Assert.Equal(10, collection[EAttribute.Strength]);
        }

        [Fact]
        public void RemoveModifier_AttributeWithNoModifiers_ReturnsFalse()
        {
            var collection = new AttributeCollection([]);

            var removed = collection.RemoveModifier(Additive(EAttribute.Intellect, 1));

            Assert.False(removed);
        }

        [Fact]
        public void AllModifiers_ReturnsAllAddedModifiers()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Strength, 10),
                Additive(EAttribute.Endurance, 5),
            };

            var collection = new AttributeCollection(modifiers);

            // Includes the 2 user modifiers + 8 static modifiers
            var allMods = collection.AllModifiers().ToList();
            Assert.True(allMods.Count >= 10);
        }

        [Fact]
        public void AllModifiers_EmptyCollection_ReturnsOnlyStaticModifiers()
        {
            // Untouched (lazily-uncreated) slots must not surface as null entries; an empty
            // collection still carries exactly the static modifier set.
            var collection = new AttributeCollection([]);

            var allMods = collection.AllModifiers().ToList();

            Assert.Equal(StaticAttributeModifiers.All.Count, allMods.Count);
            Assert.All(allMods, modifier => Assert.NotNull(modifier));
        }

        [Fact]
        public void Indexer_ReadingUntouchedAttribute_DoesNotAffectOtherAttributes()
        {
            // Reading an attribute lazily creates and caches its node; this must not leak any
            // value into unrelated slots that were never written.
            var collection = new AttributeCollection([Additive(EAttribute.Strength, 10)]);

            Assert.Equal(0, collection[EAttribute.Intellect]);
            Assert.Equal(0, collection[EAttribute.Dexterity]);
            Assert.Equal(10, collection[EAttribute.Strength]);
        }

        [Fact]
        public void Indexer_RepeatedReadOfUntouchedAttribute_IsStable()
        {
            // The first read of an empty slot creates the node and caches 0; subsequent reads
            // must return the same cached value rather than diverging.
            var collection = new AttributeCollection([]);

            Assert.Equal(0, collection[EAttribute.Luck]);
            Assert.Equal(0, collection[EAttribute.Luck]);
        }

        [Fact]
        public void AddModifier_ToAttributeAlreadyReadWhileEmpty_UpdatesValue()
        {
            // Read first (lazily creates + caches the empty node), then add — the add must
            // invalidate that cache so the next read reflects the new modifier.
            var collection = new AttributeCollection([]);

            Assert.Equal(0, collection[EAttribute.Strength]);
            collection.AddModifier(Additive(EAttribute.Strength, 8));

            Assert.Equal(8, collection[EAttribute.Strength]);
        }

        [Fact]
        public void DerivedModifier_FromOtherwiseUntouchedSource_StillCascades()
        {
            // The derived source attribute is never read or directly modified, so its node only
            // exists because the derived link lazily created it. The cascade must still fire.
            var endurance = Additive(EAttribute.Endurance, 10);
            var collection = new AttributeCollection([]);

            // MaxHealth before any Endurance: base 50 only.
            Assert.Equal(50, collection[EAttribute.MaxHealth]);

            collection.AddModifier(endurance);

            // 50 + 20*10 = 250 once Endurance feeds the static derived MaxHealth modifier.
            Assert.Equal(250, collection[EAttribute.MaxHealth]);
        }

        [Fact]
        public void SeparateCollections_DoNotShareNodeState()
        {
            // Lazy creation must give each collection its own nodes; a write to one must never
            // bleed into another (the per-instance analogue of "each slot is its own object").
            var first = new AttributeCollection([Additive(EAttribute.Strength, 10)]);
            var second = new AttributeCollection([]);

            Assert.Equal(10, first[EAttribute.Strength]);
            Assert.Equal(0, second[EAttribute.Strength]);

            second.AddModifier(Additive(EAttribute.Strength, 3));

            Assert.Equal(10, first[EAttribute.Strength]);
            Assert.Equal(3, second[EAttribute.Strength]);
        }

        private static AttributeModifier Additive(EAttribute attribute, double amount) => new()
        {
            Attribute = attribute,
            Amount = amount,
            Type = EModifierType.Additive,
            Source = EAttributeModifierSource.PlayerStatPoints,
        };
    }
}
