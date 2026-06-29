using Game.Core.Attributes;

namespace Game.Core.Proficiencies
{
    /// <summary>
    /// Maps a damage-type key (<see cref="EDamageTypeKey"/>) to the proficiency routing key
    /// (<see cref="EActivityKey"/>) a path trains on, on either side of the two-book model (spike #1318).
    /// A resolved <see cref="EDamageType"/> runs through <see cref="DamageTypes.Applies(EDamageType)"/> to a set
    /// of damage-type keys; each trains the path bound to the corresponding activity key — the <em>offense</em>
    /// key for the output book (<see cref="ForDamageKey"/>) or the <em>resist</em> key for the incoming book
    /// (<see cref="ForDamageKeyResist"/>). Each enum shares the ten damage-key members one-for-one with
    /// <see cref="EDamageTypeKey"/> — these maps pin that correspondence explicitly rather than relying on the
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

        private static readonly IReadOnlyDictionary<EDamageTypeKey, EActivityKey> ResistByDamageKey =
            new Dictionary<EDamageTypeKey, EActivityKey>
            {
                [EDamageTypeKey.Physical] = EActivityKey.PhysicalResist,
                [EDamageTypeKey.Fire] = EActivityKey.FireResist,
                [EDamageTypeKey.Water] = EActivityKey.WaterResist,
                [EDamageTypeKey.Earth] = EActivityKey.EarthResist,
                [EDamageTypeKey.Wind] = EActivityKey.WindResist,
                [EDamageTypeKey.Bleed] = EActivityKey.BleedResist,
                [EDamageTypeKey.Poison] = EActivityKey.PoisonResist,
                [EDamageTypeKey.Burn] = EActivityKey.BurnResist,
                [EDamageTypeKey.Elemental] = EActivityKey.ElementalResist,
                [EDamageTypeKey.Dot] = EActivityKey.DotResist,
            };

        /// <summary>The offense routing key a damage-type <paramref name="key"/> trains (output book).</summary>
        public static EActivityKey ForDamageKey(EDamageTypeKey key) => ByDamageKey[key];

        /// <summary>The resist routing key a damage-type <paramref name="key"/> trains (incoming book).</summary>
        public static EActivityKey ForDamageKeyResist(EDamageTypeKey key) => ResistByDamageKey[key];
    }
}
