using Game.Core.Attributes;

namespace Game.Core.Proficiencies
{
    /// <summary>
    /// (<see cref="EActivityKey"/>) a path trains on, on either side of the two-book model (spike #1318).
    /// A resolved <see cref="EDamageType"/> runs through <see cref="DamageTypes.Applies(EDamageType)"/> to a set
    /// of damage-type keys; each trains the path bound to the corresponding activity key — the <em>offense</em>
    /// key for the output book (<see cref="ForDamageKey"/>) or the <em>resist</em> key for the incoming book
    /// (<see cref="ForDamageKeyResist"/>). The enums share their damage-key members one-for-one (the eight #1320
    /// leaves, the Elemental/DoT categories, and the #1340 weapon leaves) — these maps pin that correspondence
    /// explicitly rather than relying on the enums keeping matching ordinals (they do not: the weapon keys were
    /// appended at different positions in each enum). The amplification-only weapon keys (#1340) have an
    /// <em>offense</em> key but no <em>resist</em> key — a weapon hit's exposure trains the shared
    /// <see cref="EDamageTypeKey.Physical"/> resist key only — so <see cref="ForDamageKeyResist"/> returns
    /// <c>null</c> for them.
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
                [EDamageTypeKey.Sword] = EActivityKey.Sword,
                [EDamageTypeKey.Axe] = EActivityKey.Axe,
                [EDamageTypeKey.Bow] = EActivityKey.Bow,
                [EDamageTypeKey.Club] = EActivityKey.Club,
                [EDamageTypeKey.Dagger] = EActivityKey.Dagger,
                [EDamageTypeKey.Unarmed] = EActivityKey.Unarmed,
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

        /// <summary>
        /// The resist routing key a damage-type <paramref name="key"/> trains (incoming book), or <c>null</c> for
        /// an amplification-only weapon key (#1340), whose exposure trains the shared Physical resist key instead.
        /// </summary>
        public static EActivityKey? ForDamageKeyResist(EDamageTypeKey key) =>
            ResistByDamageKey.TryGetValue(key, out var activityKey) ? activityKey : null;
    }
}
