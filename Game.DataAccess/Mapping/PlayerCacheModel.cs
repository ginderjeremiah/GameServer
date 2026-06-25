using Game.Core.Players;

namespace Game.DataAccess.Mapping
{
    /// <summary>
    /// Lean persistence model for the player aggregate. It captures only the player's own state and reduces
    /// reference data (items, item mods, skills) to ids, so it never carries a copy of mutable reference data
    /// that could go stale behind an admin edit or a build-then-swap. It is the single shape produced by both
    /// reads — the Redis cache (de)serializes it directly, and the database query projects straight into it —
    /// and <see cref="PlayerCacheMapper.ToCore"/> re-resolves the reference graph from the in-memory catalogs
    /// when rehydrating the aggregate from it (#1155).
    /// <para>
    /// Its shape mirrors the relational tables (flat applied mods, per-skill selected/order flags) so the EF
    /// projection is a trivial column copy with no correlated sub-queries, and the grouping/ordering stays in
    /// <see cref="PlayerCacheMapper.ToCore"/> where it is unit-tested. Reference-data-free value objects
    /// (<see cref="StatAllocation"/>, <see cref="LogPreference"/>) are reused directly — only the parts that
    /// embed reference data get a dedicated, id-only shape below.
    /// </para>
    /// </summary>
    internal sealed class PlayerCacheModel
    {
        public required int Id { get; init; }
        public required int ClassId { get; init; }
        public required string Name { get; init; }
        public required int Level { get; init; }
        public required int Exp { get; init; }
        public required int CurrentZoneId { get; init; }
        public required DateTime LastActivity { get; init; }
        public required bool AutoChallengeBoss { get; init; }
        public required int StatPointsGained { get; init; }
        public required int StatPointsUsed { get; init; }
        public required List<StatAllocation> StatAllocations { get; init; }
        public required List<CachedUnlockedItem> UnlockedItems { get; init; }
        public required List<CachedAppliedMod> AppliedMods { get; init; }
        public required List<int> UnlockedModIds { get; init; }
        public required List<CachedPlayerSkill> Skills { get; init; }
        public required List<LogPreference> LogPreferences { get; init; }
    }

    /// <summary>An unlocked item reduced to its id plus the player-specific state (equip slot, favorite).</summary>
    internal sealed class CachedUnlockedItem
    {
        public required int ItemId { get; init; }

        /// <summary>The equipment slot the item occupies, or null when it is unequipped.</summary>
        public required int? EquipmentSlotId { get; init; }
        public required bool Favorite { get; init; }
    }

    /// <summary>An applied mod reduced to its ids; the <see cref="Core.Items.ItemMod"/> is re-resolved on rehydration.</summary>
    internal sealed class CachedAppliedMod
    {
        /// <summary>The id of the item the mod is applied to (kept flat, mirroring the relational table).</summary>
        public required int ItemId { get; init; }
        public required int ItemModId { get; init; }
        public required int ItemModSlotId { get; init; }
    }

    /// <summary>An unlocked skill reduced to its id plus the selected/order loadout state.</summary>
    internal sealed class CachedPlayerSkill
    {
        public required int SkillId { get; init; }
        public required bool Selected { get; init; }
        public required int Order { get; init; }
    }
}
