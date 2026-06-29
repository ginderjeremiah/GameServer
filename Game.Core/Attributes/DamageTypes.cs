using static Game.Core.EAttribute;
using static Game.Core.EDamageTypeKey;

namespace Game.Core.Attributes
{
    /// <summary>
    /// The damage-type taxonomy foundation (spike #1320, Area A). The single source of truth for:
    /// the leaf-type → applicable-keys map (<see cref="Applies"/>), the key → amplification/resistance
    /// attribute pairing (<see cref="AttributesFor"/>), and the per-key display facts. It carries
    /// <em>no battle behaviour</em> — the amp/resist attributes it indexes stay inert until the damage
    /// pipeline (Area B/C) reads them (the #178 foundation pattern).
    /// </summary>
    /// <remarks>
    /// The frontend mirror (<c>$lib/battle/damage-types.ts</c>) is generated from this class by
    /// <c>Game.Api.CodeGen</c> rather than hand-maintained, so the two simulators cannot silently drift.
    /// The iteration order of <see cref="Keys"/> — and of each <see cref="Applies"/> entry — is fixed
    /// because the damage pipeline folds the amp/resist sums in that order, and float addition is not
    /// associative (a parity contract).
    /// </remarks>
    public static class DamageTypes
    {
        /// <summary>
        /// The per-key foundation facts: the amplification/resistance attribute pair the key backs and the
        /// display metadata (<see cref="Attribute"/> sources its amp/resist codes and descriptions from these).
        /// </summary>
        /// <param name="Key">The damage-type key.</param>
        /// <param name="Label">The lower-case noun phrase used in the amp/resist attribute descriptions.</param>
        /// <param name="Code">The short display code stem (the attribute code is this plus <c>AMP</c>/<c>RES</c>).</param>
        /// <param name="Amplification">The attacker-side amplification attribute keyed here.</param>
        /// <param name="Resistance">
        /// The defender-side resistance attribute keyed here, or <c>null</c> for an <em>amplification-only</em> key
        /// (the #1340 weapon leaves carry no per-weapon resistance — a weapon hit mitigates via the shared
        /// <see cref="EDamageTypeKey.Physical"/> key instead).
        /// </param>
        public readonly record struct KeyInfo(
            EDamageTypeKey Key,
            string Label,
            string Code,
            EAttribute Amplification,
            EAttribute? Resistance);

        // The keys in canonical order: the eight #1320 leaf types, the two cross-cutting categories, then the
        // #1340 weapon leaves (appended to match the append-only enum order). The single ordered list every other
        // structure here is derived from. The weapon keys are amplification-only — a null Resistance.
        private static readonly IReadOnlyList<KeyInfo> KeyInfos =
        [
            new(Physical, "physical", "PHY", PhysicalAmplification, PhysicalResistance),
            new(Fire, "fire", "FIR", FireAmplification, FireResistance),
            new(Water, "water", "WAT", WaterAmplification, WaterResistance),
            new(Earth, "earth", "EAR", EarthAmplification, EarthResistance),
            new(Wind, "wind", "WND", WindAmplification, WindResistance),
            new(Bleed, "bleed", "BLD", BleedAmplification, BleedResistance),
            new(Poison, "poison", "PSN", PoisonAmplification, PoisonResistance),
            new(Burn, "burn", "BRN", BurnAmplification, BurnResistance),
            new(Elemental, "elemental", "ELE", ElementalAmplification, ElementalResistance),
            new(Dot, "damage-over-time", "DOT", DotAmplification, DotResistance),
            new(Sword, "sword", "SWD", SwordAmplification, null),
            new(Axe, "axe", "AXE", AxeAmplification, null),
            new(Bow, "bow", "BOW", BowAmplification, null),
            new(Club, "club", "CLB", ClubAmplification, null),
            new(Dagger, "dagger", "DAG", DaggerAmplification, null),
            new(Unarmed, "unarmed", "UNA", UnarmedAmplification, null),
        ];

        /// <summary>
        /// The per-second accumulator attribute backing one DoT leaf type (spike #1320, Area C). The DoT type
        /// is encoded by <em>which accumulator an effect targets</em> — there is no type field on a skill
        /// effect — so this pairing is the single source linking the two.
        /// </summary>
        /// <param name="Type">The DoT leaf damage type.</param>
        /// <param name="Accumulator">The per-second <see cref="EAttribute"/> the type accumulates into.</param>
        public readonly record struct DotAccumulatorInfo(EDamageType Type, EAttribute Accumulator);

        // The three DoT accumulators in the fixed iteration order the end-of-tick DoT phase folds them in
        // (float addition is not associative, so the order is a parity contract). Bleed reuses the slot of the
        // former single DamageTakenPerSecond channel; poison/burn append after the amp/resist block.
        private static readonly IReadOnlyList<DotAccumulatorInfo> DotAccumulatorList =
        [
            new(EDamageType.Bleed, BleedDamagePerSecond),
            new(EDamageType.Poison, PoisonDamagePerSecond),
            new(EDamageType.Burn, BurnDamagePerSecond),
        ];

        private static readonly IReadOnlyDictionary<EAttribute, EDamageType> DotTypeByAccumulator =
            DotAccumulatorList.ToDictionary(info => info.Accumulator, info => info.Type);

        // Leaf type → applicable keys (the spike's taxonomy table). A leaf type's own key comes first, then any
        // cross-cutting categories it belongs to; the order is fixed for float parity.
        private static readonly IReadOnlyDictionary<EDamageType, IReadOnlyList<EDamageTypeKey>> AppliesMap =
            new Dictionary<EDamageType, IReadOnlyList<EDamageTypeKey>>
            {
                [EDamageType.Physical] = [Physical],
                [EDamageType.Fire] = [Fire, Elemental],
                [EDamageType.Water] = [Water, Elemental],
                [EDamageType.Earth] = [Earth, Elemental],
                [EDamageType.Wind] = [Wind, Elemental],
                [EDamageType.Bleed] = [Bleed, Dot],
                [EDamageType.Poison] = [Poison, Dot],
                [EDamageType.Burn] = [Burn, Fire, Elemental, Dot],
                // Weapon leaves (#1340): the weapon's own (amplification-only) key, then the shared Physical key
                // that carries its amplification AND its resistance (no per-weapon resistance).
                [EDamageType.Sword] = [Sword, Physical],
                [EDamageType.Axe] = [Axe, Physical],
                [EDamageType.Bow] = [Bow, Physical],
                [EDamageType.Club] = [Club, Physical],
                [EDamageType.Dagger] = [Dagger, Physical],
                [EDamageType.Unarmed] = [Unarmed, Physical],
            };

        private static readonly IReadOnlyDictionary<EDamageTypeKey, KeyInfo> InfoByKey =
            KeyInfos.ToDictionary(info => info.Key);

        // Both the amplification and (where it exists) resistance attribute map back to the key. An
        // amplification-only weapon key contributes only its amplification.
        private static readonly IReadOnlyDictionary<EAttribute, EDamageTypeKey> KeyByAttribute =
            KeyInfos
                .SelectMany(info => info.Resistance is EAttribute resistance
                    ? new[] { (info.Amplification, info.Key), (resistance, info.Key) }
                    : [(info.Amplification, info.Key)])
                .ToDictionary(pair => pair.Item1, pair => pair.Item2);

        // Precomputed amp/resist attribute lists per leaf type, so the per-hit lookup on the battle hot path
        // is an O(1) dictionary read with no per-call allocation.
        private static readonly IReadOnlyDictionary<EDamageType, IReadOnlyList<EAttribute>> AmplificationByType =
            AppliesMap.ToDictionary(e => e.Key, e => (IReadOnlyList<EAttribute>)e.Value.Select(k => InfoByKey[k].Amplification).ToArray());

        // The amplification-only weapon keys drop out here (their null Resistance is filtered), so a weapon hit's
        // resistance list is just the shared Physical key — physical resistance is its only mitigation lever.
        private static readonly IReadOnlyDictionary<EDamageType, IReadOnlyList<EAttribute>> ResistanceByType =
            AppliesMap.ToDictionary(e => e.Key, e => (IReadOnlyList<EAttribute>)e.Value.Select(k => InfoByKey[k].Resistance).OfType<EAttribute>().ToArray());

        /// <summary>The leaf damage types, in enum order.</summary>
        public static IReadOnlyList<EDamageType> LeafTypes { get; } = Enum.GetValues<EDamageType>();

        /// <summary>The damage-type keys, in canonical iteration order.</summary>
        public static IReadOnlyList<KeyInfo> Keys => KeyInfos;

        /// <summary>
        /// The three DoT (type, per-second accumulator) pairings in the fixed order the end-of-tick DoT phase
        /// iterates them — the single source linking a DoT leaf type to the accumulator that encodes it.
        /// </summary>
        public static IReadOnlyList<DotAccumulatorInfo> DotAccumulators => DotAccumulatorList;

        /// <summary>
        /// The set of keys whose amplification/resistance apply to a hit of the given leaf <paramref name="type"/> —
        /// the leaf type itself plus any cross-cutting categories — in fixed iteration order.
        /// </summary>
        public static IReadOnlyList<EDamageTypeKey> Applies(EDamageType type)
        {
            return AppliesMap[type];
        }

        /// <summary>
        /// The amplification / resistance attribute pair a <paramref name="key"/> backs. The resistance is
        /// <c>null</c> for an amplification-only weapon key (#1340).
        /// </summary>
        public static (EAttribute Amplification, EAttribute? Resistance) AttributesFor(EDamageTypeKey key)
        {
            return (InfoByKey[key].Amplification, InfoByKey[key].Resistance);
        }

        /// <summary>
        /// The attacker-side amplification attributes summed for a hit of the given leaf <paramref name="type"/>,
        /// in fixed iteration order (per-hit lookup helper for the damage pipeline).
        /// </summary>
        public static IReadOnlyList<EAttribute> AmplificationAttributes(EDamageType type)
        {
            return AmplificationByType[type];
        }

        /// <summary>
        /// The defender-side resistance attributes summed for a hit of the given leaf <paramref name="type"/>,
        /// in fixed iteration order (per-hit lookup helper for the damage pipeline).
        /// </summary>
        public static IReadOnlyList<EAttribute> ResistanceAttributes(EDamageType type)
        {
            return ResistanceByType[type];
        }

        /// <summary>
        /// The damage-type key an amplification/resistance <paramref name="attribute"/> belongs to, or
        /// <c>null</c> when it is not an amp/resist attribute. Drives the breakdown screen's by-type grouping.
        /// </summary>
        public static EDamageTypeKey? KeyForAttribute(EAttribute attribute)
        {
            return KeyByAttribute.TryGetValue(attribute, out var key) ? key : null;
        }

        /// <summary>
        /// The DoT leaf type a per-second <paramref name="attribute"/> accumulates, or <c>null</c> when it is
        /// not a DoT accumulator. Lets the effect-apply path detect a DoT effect and freeze the caster's typed
        /// amplification into the accumulated magnitude (spike #1320, Area C).
        /// </summary>
        public static EDamageType? DotTypeForAccumulator(EAttribute attribute)
        {
            return DotTypeByAccumulator.TryGetValue(attribute, out var type) ? type : null;
        }

        /// <summary>The per-key foundation facts for <paramref name="key"/>.</summary>
        public static KeyInfo Info(EDamageTypeKey key)
        {
            return InfoByKey[key];
        }
    }
}
