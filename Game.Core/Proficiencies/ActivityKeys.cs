using Game.Core.Attributes;

namespace Game.Core.Proficiencies
{
    /// <summary>
    /// Maps a damage-type key (<see cref="EDamageTypeKey"/>) to the proficiency routing key
    /// (<see cref="EActivityKey"/>) a path trains on. Offense activity routes a skill's resolved
    /// <see cref="EDamageType"/> through <see cref="DamageTypes.Applies(EDamageType)"/> to a set of damage-type
    /// keys; each trains the path bound to the corresponding activity key. The two enums share the ten
    /// damage-key members one-for-one — this pins that correspondence explicitly rather than relying on the two
    /// enums keeping matching ordinals.
    /// </summary>
    public static class ActivityKeys
    {
        private static readonly IReadOnlyDictionary<EDamageTypeKey, EActivityKey> ByDamageKey =
            new Dictionary<EDamageTypeKey, EActivityKey>
            {
                [EDamageTypeKey.Physical] = EActivityKey.Physical,
                [EDamageTypeKey.Fire] = EActivityKey.Fire,
                [EDamageTypeKey.Water] = EActivityKey.Water,
                [EDamageTypeKey.Earth] = EActivityKey.Earth,
                [EDamageTypeKey.Wind] = EActivityKey.Wind,
                [EDamageTypeKey.Bleed] = EActivityKey.Bleed,
                [EDamageTypeKey.Poison] = EActivityKey.Poison,
                [EDamageTypeKey.Burn] = EActivityKey.Burn,
                [EDamageTypeKey.Elemental] = EActivityKey.Elemental,
                [EDamageTypeKey.Dot] = EActivityKey.Dot,
            };

        /// <summary>The proficiency routing key a damage-type <paramref name="key"/> trains.</summary>
        public static EActivityKey ForDamageKey(EDamageTypeKey key) => ByDamageKey[key];
    }
}
