using Game.Core.Attributes;
using Game.Core.Attributes.Modifiers;
using Game.Core.Classes;
using Game.Core.Items;
using Game.Core.Players;
using Game.Core.Proficiencies;
using Game.Core.Skills;

namespace Game.Core.Battle
{
    /// <summary>
    /// A minimal snapshot of the player's battle-relevant state captured at battle start.
    /// Stores IDs and raw allocations so the full <see cref="Battler"/> can be reconstructed
    /// from catalog data at simulation time, even if the player mutates gear or skills mid-battle.
    /// </summary>
    public class BattleSnapshot
    {
        /// <summary>
        /// The player's level at battle start.
        /// </summary>
        public required int Level { get; set; }

        /// <summary>
        /// The id of the player's class at battle start, or <c>null</c> for a snapshot built without class
        /// state (e.g. a hand-built test snapshot). The class's <see cref="Class.AttributeDistributions"/>
        /// resolve into the level-scaled, non-reallocatable locked base — the class attribute fingerprint —
        /// at battler assembly (spike #1126 area D), exactly like an enemy's distributions. Derived purely from
        /// <c>(class, <see cref="Level"/>)</c>, never stored, so it re-tunes for free when growth vectors change.
        /// </summary>
        public int? ClassId { get; set; }

        /// <summary>
        /// The raw stat allocations (core attributes) at battle start.
        /// </summary>
        public required List<StatAllocation> StatAllocations { get; set; }

        /// <summary>
        /// The equipped items and their applied mod IDs at battle start.
        /// </summary>
        public required List<EquippedItemSnapshot> EquippedItems { get; set; }

        /// <summary>
        /// The IDs of the player's selected skills at battle start.
        /// </summary>
        public required List<int> SkillIds { get; set; }

        /// <summary>
        /// The player's proficiency levels at battle start (one entry per proficiency the player has trained).
        /// Captured so the per-level/milestone attribute bonuses bake into the snapshot exactly like the stat
        /// allocations — a level gained while idling takes effect on the next battle. Like the snapshot's
        /// captured player <see cref="Level"/> (#1601), the offline simulator grows a working copy of these
        /// mid-window (#1602); the snapshot's own list stays the frozen window-start capture. Defaults to
        /// empty so a snapshot built without proficiency state carries no proficiency bonus.
        /// </summary>
        public List<ProficiencyLevelSnapshot> ProficiencyLevels { get; set; } = [];

        /// <summary>
        /// Captures a player's current battle-relevant state as a minimal ID-based snapshot. The
        /// projection lives in the domain alongside <see cref="ToBattler"/> so the capture/reconstruct
        /// pair stays a single, self-consistent unit.
        /// </summary>
        public static BattleSnapshot FromPlayer(Player player, IEnumerable<ProficiencyLevelSnapshot>? proficiencyLevels = null)
        {
            var equippedItems = player.Inventory.EquipmentSlots
                .SelectNotNull(slot => slot.ItemId)
                .Select(itemId =>
                {
                    // Applied mods live on the player's UnlockedItemSlot, so capture their IDs from there.
                    // An equipped item must always have a matching unlocked entry (TryEquipItem enforces
                    // this), so a missing entry means the inventory invariant is broken. Fail loudly rather
                    // than silently capturing the item without its mods: this snapshot is the anti-cheat
                    // parity surface, and a quiet capture would later validate a replay against weaker
                    // attributes than the client simulated, failing legitimate victories with no signal.
                    var unlocked = player.Inventory.GetUnlockedItem(itemId)
                        ?? throw new InvalidOperationException(
                            $"Equipped item {itemId} has no matching entry in the player's unlocked items.");

                    var modIds = unlocked.AppliedMods
                        .Select(m => m.ItemModId)
                        .ToList();

                    return new EquippedItemSnapshot
                    {
                        ItemId = itemId,
                        AppliedModIds = modIds,
                    };
                })
                .ToList();

            return new BattleSnapshot
            {
                Level = player.Level,
                // The class is permanent, so its id is captured straight off the aggregate; the locked-base
                // distribution it drives is re-derived from (class, level) at assembly rather than stored.
                ClassId = player.ClassId,
                // Copy each allocation so a later in-place stat reallocation on the live player cannot
                // retroactively mutate this snapshot, consistent with the other projected fields.
                StatAllocations = player.StatPoints.StatAllocations.Select(allocation => allocation.Copy()).ToList(),
                EquippedItems = equippedItems,
                SkillIds = player.SelectedSkills.Select(s => s.Id).ToList(),
                // Proficiency progress lives on the separate PlayerProgress aggregate, so the caller supplies
                // the captured levels (it owns the progress read); absent for callers with no proficiency state.
                ProficiencyLevels = proficiencyLevels?.ToList() ?? [],
            };
        }

        /// <summary>
        /// Reconstructs the player's <see cref="Battler"/> from this snapshot, resolving the captured IDs
        /// against the in-memory catalogs via the supplied resolvers. The same modifier-composition primitives
        /// production uses (<see cref="StatAllocation.ToModifier"/> and <see cref="Item.GetAttributeModifiers"/>)
        /// are reused here, so a battle validated from a snapshot is guaranteed to match the player's live
        /// attributes — the frontend/backend battle-parity guarantee. The caller provides the resolvers so the
        /// domain stays independent of the data-access layer that owns catalog lookups, mirroring
        /// <see cref="BattleFactory"/>'s enemy resolver.
        /// </summary>
        public Battler ToBattler(
            Func<int, Item> resolveItem, Func<int, ItemMod> resolveMod, Func<int, Skill?> resolveSkill,
            Func<int, Proficiency>? resolveProficiency = null, Func<int, Class>? resolveClass = null) =>
            GetBattlerMaterials(resolveItem, resolveMod, resolveSkill, resolveProficiency, resolveClass).Build();

        /// <summary>
        /// Composes this snapshot's battler assembly ingredients without the final, always-fresh
        /// <see cref="AttributeCollection"/>/<see cref="Battler"/> construction — everything <see cref="ToBattler"/>
        /// otherwise redoes on every call. A <see cref="Battler"/> is single-use (simulation mutates its health
        /// and active effects), but the resolver/LINQ composition behind it is invariant for as long as this
        /// snapshot's <see cref="StatAllocations"/>, <see cref="EquippedItems"/>, <see cref="SkillIds"/>,
        /// <see cref="Level"/>, and <see cref="ProficiencyLevels"/> are — so a caller replaying many battles
        /// against the same snapshot state (the offline simulator, #1730) can cache the returned
        /// <see cref="BattlerMaterials"/> and call <see cref="BattlerMaterials.Build"/> per battle instead of
        /// re-walking this composition every time.
        /// </summary>
        public BattlerMaterials GetBattlerMaterials(
            Func<int, Item> resolveItem, Func<int, ItemMod> resolveMod, Func<int, Skill?> resolveSkill,
            Func<int, Proficiency>? resolveProficiency = null, Func<int, Class>? resolveClass = null)
        {
            var baseModifiers = GetModifiers(resolveItem, resolveMod, resolveProficiency, resolveClass).ToList();
            var signaturePassiveDefinition = ResolveSignaturePassiveDefinition(resolveClass);
            var signaturePassive = signaturePassiveDefinition?.GetModifier(new AttributeCollection(baseModifiers).GetAttributeValue);

            // The weapon-match gate reads each fielded id's type via the same resolver; an id that resolves to
            // no skill (a leftover/unauthored grant such as an unseeded punch) yields a null type and is dropped
            // by OrderSkillIds, so the following Select only ever sees ids that resolve (OfType filters the
            // never-null nullable away without a null-forgiving operator).
            var skills = GetBattleSkillIds(resolveItem, id => resolveSkill(id)?.PrimaryDamageType)
                .Select(resolveSkill)
                .OfType<Skill>()
                .ToList();

            // The parry counter skill (#1457): the equipped weapon's signature — the virtual fists' punch
            // bare-handed — resolved once at assembly like the weapon-match gate. An unresolvable id (an
            // unauthored punch) yields null, so a parry then negates without a riposte rather than firing a
            // phantom skill, mirroring how OrderSkillIds drops an unresolvable grant.
            var weapon = ResolveEquippedWeapon(resolveItem);
            var counterSkill = resolveSkill(weapon?.GrantedSkillId ?? GameConstants.PunchSkillId);

            return new BattlerMaterials(baseModifiers, signaturePassive, signaturePassiveDefinition, skills, counterSkill, Level);
        }

        /// <summary>
        /// The equipped <see cref="EItemCategory.Weapon"/> item captured on this snapshot, or <c>null</c> for
        /// bare hands (the virtual <c>Unarmed</c> fists). Shared by the battle-skill assembly and the parry
        /// counter resolution so the two read the same weapon.
        /// </summary>
        private Item? ResolveEquippedWeapon(Func<int, Item> resolveItem)
        {
            return EquippedItems
                .Select(equipped => resolveItem(equipped.ItemId))
                .FirstOrDefault(item => item.Category == EItemCategory.Weapon);
        }

        /// <summary>
        /// Resolves the captured class's signature passive against the already-assembled
        /// <paramref name="attributes"/> as the final contributor (spike #1126 area E), or <c>null</c> when the
        /// snapshot carries no class state (a hand-built test snapshot). It is resolved against — and added
        /// <b>last</b>, after — the free pool, gear, locked base, proficiency bonuses, and the static engine
        /// modifiers, so an attribute-scaled passive reads the fully-resolved value of its scaling attribute (the
        /// snapshot state a V1 passive sees, like a skill effect reading its caster), and so the frontend mirror
        /// (which adds it after building the battler) lands it in the identical place in the per-attribute apply
        /// order — floating-point addition is not associative, so the anti-cheat replay depends on that order
        /// matching bit-for-bit. A captured <see cref="ClassId"/> requires a resolver, failing loudly rather than
        /// silently dropping the passive (mirroring the locked-base guard in <see cref="GetModifiers"/>).
        /// </summary>
        private AttributeModifier? ResolveSignaturePassive(AttributeCollection attributes, Func<int, Class>? resolveClass)
        {
            return ResolveSignaturePassiveDefinition(resolveClass)?.GetModifier(attributes.GetAttributeValue);
        }

        /// <summary>
        /// The captured class's signature-passive <b>definition</b> (not yet resolved against any attributes),
        /// or <c>null</c> when the snapshot carries no class state. Split out from <see cref="ResolveSignaturePassive"/>
        /// so <see cref="GetBattlerMaterials"/> can hand the definition itself to <see cref="BattlerMaterials"/> —
        /// which threads it onto the built <see cref="Battler"/> so <see cref="Battler.CloneWithAttributeDelta"/>
        /// can re-resolve an attribute-scaled passive against a bumped clone (#1862) — while still resolving the
        /// modifier once here for the battler's own initial assembly.
        /// </summary>
        private ClassSignaturePassive? ResolveSignaturePassiveDefinition(Func<int, Class>? resolveClass)
        {
            if (ClassId is not int classId)
            {
                return null;
            }

            if (resolveClass is null)
            {
                throw new InvalidOperationException(
                    "A battle snapshot with a captured class requires a class resolver to compose its signature passive.");
            }

            return resolveClass(classId).SignaturePassive;
        }

        /// <summary>
        /// The snapshot's battle modifiers (<see cref="GetModifiers"/>) <b>plus</b> the resolved signature
        /// passive — the legacy <c>SumCoreAttributes</c> power measurement read this (so a class built around a
        /// large flat-core passive could not under-report its power and inflate rewards). The live reward path
        /// no longer calls this: <see cref="DefeatRewards"/> now rates the <see cref="Battler"/> from
        /// <see cref="ToBattler"/> directly, which already composes the signature passive as its own final step
        /// (spike #1526). It has no remaining production caller — retained because the combat-rating
        /// calibration report's integration test (#1533) still builds its old-measure reference builds from
        /// this legacy modifier-list shape, and the snapshot unit tests exercise it directly. A transient
        /// <see cref="AttributeCollection"/> resolves the passive's scaling against the same fully-assembled
        /// attributes <see cref="ToBattler"/> uses, so the two stay consistent.
        /// </summary>
        public IEnumerable<AttributeModifier> GetModifiersWithSignaturePassive(
            Func<int, Item> resolveItem, Func<int, ItemMod> resolveMod,
            Func<int, Proficiency>? resolveProficiency = null, Func<int, Class>? resolveClass = null)
        {
            var modifiers = GetModifiers(resolveItem, resolveMod, resolveProficiency, resolveClass).ToList();
            if (ResolveSignaturePassive(new AttributeCollection(modifiers), resolveClass) is { } passive)
            {
                modifiers.Add(passive);
            }
            return modifiers;
        }

        /// <summary>
        /// The ordered, de-duplicated, weapon-gated ids of the skills this snapshot's battler fights with: the
        /// captured selected skills first, then the skills granted by the equipped items in
        /// <see cref="EEquipmentSlot"/> order (the order <see cref="FromPlayer"/> captured them in). The
        /// granted ids — and the equipped weapon's type — are derived here from the already-captured
        /// <see cref="EquippedItems"/> (no extra snapshot field), exactly as the item attributes are.
        /// <para>
        /// The equipped weapon is the equipped <see cref="EItemCategory.Weapon"/> item; its type resolves as
        /// <c>weapon?.WeaponType ?? Unarmed</c> (an empty slot is the virtual <c>Unarmed</c> "fists"). With no
        /// weapon equipped, the fists' granted signature — the configured <see cref="GameConstants.PunchSkillId"/>
        /// — is appended, so punch is fielded only bare-handed (a real <c>Unarmed</c> weapon fields its own
        /// signature instead, no free bonus skill). Ordering, de-duplication, and the weapon-match gate are
        /// delegated to <see cref="BattleLoadout.OrderSkillIds"/>, the single rule the frontend mirrors for parity.
        /// </para>
        /// </summary>
        public IEnumerable<int> GetBattleSkillIds(Func<int, Item> resolveItem, Func<int, EDamageType?> resolveSkillType)
        {
            var equippedItems = EquippedItems.Select(equipped => resolveItem(equipped.ItemId)).ToList();
            var weapon = equippedItems.FirstOrDefault(item => item.Category == EItemCategory.Weapon);
            var equippedWeaponType = weapon?.WeaponType ?? EDamageType.Unarmed;

            var grantedSkillIds = equippedItems.SelectNotNull(item => item.GrantedSkillId);
            // Bare hands are the virtual Unarmed "fists" weapon whose signature is punch (spike #1342): with no
            // weapon equipped, the weapon slot's granted signature is the configured punch skill. It rides the
            // ordinary grant list, so the uniform weapon-match gate fields it (Unarmed-typed, bare hands read
            // Unarmed) and the no-stranding invariant holds even when every selected skill is dimmed.
            if (weapon is null)
            {
                grantedSkillIds = grantedSkillIds.Append(GameConstants.PunchSkillId);
            }

            return BattleLoadout.OrderSkillIds(SkillIds, grantedSkillIds, equippedWeaponType, resolveSkillType);
        }

        /// <summary>
        /// Composes the player's battle attribute modifiers from this snapshot — the captured stat
        /// allocations, each equipped item's attributes and those of its applied mods, and the per-level/
        /// milestone bonuses of the captured proficiency levels — resolving the captured ids against the
        /// in-memory catalogs. Shared by <see cref="ToBattler"/> (the simulation) and the exp-reward power
        /// measurement (<see cref="DefeatRewards"/>), so both read the player's power from the same frozen
        /// snapshot rather than the live aggregate.
        /// <para>
        /// The composition order is part of the frontend/backend parity contract: stat allocations (the free
        /// pool), then gear, then the class locked base, then the proficiency bonuses — all additive, but
        /// floating-point addition is not associative, so the order is mirrored bit-for-bit by the frontend
        /// (<c>BattleAttributes.setData</c> places the class locked base and proficiency bonuses in its
        /// <c>additionalModifiers</c> in this same order, before the static engine modifiers). The
        /// <paramref name="resolveProficiency"/> and <paramref name="resolveClass"/> resolvers are required
        /// only when the corresponding state was captured (a captured <see cref="ClassId"/> always needs one,
        /// since every real player has a class).
        /// </summary>
        public IEnumerable<AttributeModifier> GetModifiers(
            Func<int, Item> resolveItem, Func<int, ItemMod> resolveMod,
            Func<int, Proficiency>? resolveProficiency = null, Func<int, Class>? resolveClass = null)
        {
            var modifiers = StatAllocations.Select(allocation => allocation.ToModifier())
                .Concat(EquippedItems.SelectMany(equipped =>
                    resolveItem(equipped.ItemId)
                        .GetAttributeModifiers(equipped.AppliedModIds.Select(resolveMod))));

            // The class locked base: the class's attribute fingerprint scaled to the captured level, via the
            // same BaseAmount + AmountPerLevel × level math an enemy's distributions use. A captured class
            // always requires a resolver — fail loudly rather than silently dropping the locked base, which
            // would validate a replay against weaker attributes than the client simulated (as the proficiency
            // guard below does).
            if (ClassId is int classId)
            {
                if (resolveClass is null)
                {
                    throw new InvalidOperationException(
                        "A battle snapshot with a captured class requires a class resolver to compose its locked-base distribution.");
                }

                modifiers = modifiers.Concat(resolveClass(classId).AttributeDistributions
                    .Select(distribution => distribution.GetDistributionModifier(Level)));
            }

            if (ProficiencyLevels.Count == 0)
            {
                return modifiers;
            }

            if (resolveProficiency is null)
            {
                throw new InvalidOperationException(
                    "A battle snapshot with captured proficiency levels requires a proficiency resolver to compose its bonuses.");
            }

            return modifiers.Concat(ProficiencyLevels.SelectMany(captured =>
                resolveProficiency(captured.ProficiencyId).ModifiersForLevel(captured.Level)));
        }
    }

    /// <summary>
    /// Captures a player's level in one proficiency at battle start — the input the per-level/milestone
    /// attribute bonuses are resolved from at battler assembly (<see cref="BattleSnapshot.GetModifiers"/>).
    /// </summary>
    public class ProficiencyLevelSnapshot
    {
        /// <summary>The id of the proficiency.</summary>
        public required int ProficiencyId { get; set; }

        /// <summary>The player's level in that proficiency at battle start.</summary>
        public required int Level { get; set; }

        /// <summary>
        /// The player's residual XP within <see cref="Level"/> at battle start. Unused by battle-modifier
        /// composition (<see cref="Proficiency.ModifiersForLevel"/> reads only <see cref="Level"/>) — carried
        /// so the offline simulator's in-loop proficiency accrual (#1602) has a real starting point to grow
        /// from, mirroring how <see cref="Offline.OfflineSimulationParameters.StartingExp"/> seeds level growth.
        /// Defaults to 0 for a caller that captures level only.
        /// </summary>
        public decimal Xp { get; set; }
    }

    /// <summary>
    /// Captures an equipped item and which mods were applied to it at battle start.
    /// </summary>
    public class EquippedItemSnapshot
    {
        /// <summary>
        /// The ID of the equipped item.
        /// </summary>
        public required int ItemId { get; set; }

        /// <summary>
        /// The IDs of item mods applied to this item at battle start.
        /// </summary>
        public required List<int> AppliedModIds { get; set; }
    }

    /// <summary>
    /// The reusable ingredients of a <see cref="BattleSnapshot"/>'s battler assembly — its composed base
    /// attribute modifiers, resolved signature passive, fielded skills, and counter skill — everything
    /// <see cref="BattleSnapshot.ToBattler"/> composes except the final, always-fresh
    /// <see cref="AttributeCollection"/>/<see cref="Battler"/> construction (<see cref="Build"/>). See
    /// <see cref="BattleSnapshot.GetBattlerMaterials"/> for when it is safe to cache and reuse an instance.
    /// </summary>
    public sealed class BattlerMaterials(
        IReadOnlyList<AttributeModifier> baseModifiers, AttributeModifier? signaturePassive,
        ClassSignaturePassive? signaturePassiveDefinition, IReadOnlyList<Skill> skills, Skill? counterSkill, int level)
    {
        /// <summary>Builds a fresh, single-use <see cref="Battler"/> from these materials.</summary>
        public Battler Build()
        {
            var attributes = new AttributeCollection(baseModifiers);
            if (signaturePassive is not null)
            {
                attributes.AddModifier(signaturePassive);
            }

            return new Battler(attributes, skills, level, counterSkill, signaturePassiveDefinition);
        }
    }
}
