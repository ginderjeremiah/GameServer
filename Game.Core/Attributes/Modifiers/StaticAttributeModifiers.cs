using static Game.Core.EAttribute;
using static Game.Core.EAttributeModifierSource;
using static Game.Core.EModifierType;

namespace Game.Core.Attributes.Modifiers
{
    internal static class StaticAttributeModifiers
    {
        #region CooldownRecovery
        public static AttributeModifier CooldownRecoveryAgility { get; } = new AttributeModifier
        {
            Attribute = CooldownRecovery,
            Amount = 0.4,
            Source = Derived,
            DerivedSource = Agility,
            Type = Additive,
        };

        public static AttributeModifier CooldownRecoveryDexterity { get; } = new AttributeModifier
        {
            Attribute = CooldownRecovery,
            Amount = 0.1,
            Source = Derived,
            DerivedSource = Dexterity,
            Type = Additive,
        };
        #endregion

        #region Defense
        public static AttributeModifier DefenseBase { get; } = new AttributeModifier
        {
            Attribute = Defense,
            Amount = 2.0,
            Source = BaseValue,
            Type = Additive,
        };

        public static AttributeModifier DefenseEndurance { get; } = new AttributeModifier
        {
            Attribute = Defense,
            Amount = 1.0,
            Source = Derived,
            DerivedSource = Endurance,
            Type = Additive,
        };

        public static AttributeModifier DefenseAgility { get; } = new AttributeModifier
        {
            Attribute = Defense,
            Amount = 0.5,
            Source = Derived,
            DerivedSource = Agility,
            Type = Additive,
        };
        #endregion

        #region DropBonus
        public static AttributeModifier DropBonusLuck { get; } = new AttributeModifier
        {
            Attribute = DropBonus,
            Amount = 1.0,
            Source = Derived,
            DerivedSource = Luck,
            Type = Additive,
        };
        #endregion

        #region MaxHealth
        public static AttributeModifier MaxHealthBase { get; } = new AttributeModifier
        {
            Attribute = MaxHealth,
            Amount = 50.0,
            Source = BaseValue,
            Type = Additive,
        };

        public static AttributeModifier MaxHealthEndurance { get; } = new AttributeModifier
        {
            Attribute = MaxHealth,
            Amount = 20.0,
            Source = Derived,
            DerivedSource = Endurance,
            Type = Additive,
        };

        public static AttributeModifier MaxHealthStrength { get; } = new AttributeModifier
        {
            Attribute = MaxHealth,
            Amount = 5.0,
            Source = Derived,
            DerivedSource = Strength,
            Type = Additive,
        };
        #endregion

    }
}
