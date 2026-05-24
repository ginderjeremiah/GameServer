using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;

namespace Game.Core.Tests.Attributes
{
    [TestClass]
    public class AttributeCollectionTests
    {
        [TestMethod]
        public void Indexer_AdditiveModifiers_SumsCorrectly()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Strength, 10),
                Additive(EAttribute.Strength, 5),
            };

            var collection = new AttributeCollection(modifiers);

            Assert.AreEqual(15, collection[EAttribute.Strength]);
        }

        [TestMethod]
        public void Indexer_NoModifiers_ReturnsZero()
        {
            var collection = new AttributeCollection([]);

            Assert.AreEqual(0, collection[EAttribute.Intellect]);
        }

        [TestMethod]
        public void Indexer_DerivedModifier_MultipliesBaseAttribute()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Endurance, 10),
            };

            var collection = new AttributeCollection(modifiers);

            // MaxHealth has static derived modifiers: base 50 + Endurance*20 + Strength*5
            // With Endurance=10, Strength=0: MaxHealth = 50 + 10*20 + 0*5 = 250
            Assert.AreEqual(250, collection[EAttribute.MaxHealth]);
        }

        [TestMethod]
        public void Indexer_MultipleBaseStats_DerivedComputesFromAll()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Endurance, 10),
                Additive(EAttribute.Strength, 5),
            };

            var collection = new AttributeCollection(modifiers);

            // MaxHealth = base(50) + Endurance(10)*20 + Strength(5)*5 = 50 + 200 + 25 = 275
            Assert.AreEqual(275, collection[EAttribute.MaxHealth]);
        }

        [TestMethod]
        public void Indexer_Defense_IncludesBaseAndDerived()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Endurance, 10),
                Additive(EAttribute.Agility, 4),
            };

            var collection = new AttributeCollection(modifiers);

            // Defense = base(2) + Endurance(10)*1.0 + Agility(4)*0.5 = 2 + 10 + 2 = 14
            Assert.AreEqual(14, collection[EAttribute.Defense]);
        }

        [TestMethod]
        public void Indexer_CooldownRecovery_DerivedFromAgilityAndDexterity()
        {
            var modifiers = new List<AttributeModifier>
            {
                Additive(EAttribute.Agility, 10),
                Additive(EAttribute.Dexterity, 20),
            };

            var collection = new AttributeCollection(modifiers);

            // CooldownRecovery = Agility(10)*0.4 + Dexterity(20)*0.1 = 4 + 2 = 6
            Assert.AreEqual(6, collection[EAttribute.CooldownRecovery]);
        }

        [TestMethod]
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

        [TestMethod]
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

            Assert.AreEqual(10, before);
            Assert.AreEqual(15, after);
        }

        [TestMethod]
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
            Assert.IsTrue(allMods.Count >= 10);
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
