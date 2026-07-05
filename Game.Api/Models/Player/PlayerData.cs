using Game.Abstractions.Contracts;
using Game.Api.Models.InventoryItems;
using CorePlayer = Game.Core.Players.Player;

namespace Game.Api.Models.Player
{
    public class PlayerData : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public required List<BattlerAttribute> Attributes { get; set; }
        public required List<UnlockedSkill> UnlockedSkills { get; set; }
        public int CurrentZone { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }
        public required List<LogPreference> LogPreferences { get; set; }
        public required InventoryData InventoryData { get; set; }

        /// <summary>
        /// The player's class attribute fingerprint — the distributions the live frontend battler resolves
        /// into the level-scaled, non-reallocatable locked base (spike #1126 area D), so its battle attributes
        /// match the backend snapshot (<c>BattleSnapshot.GetModifiers</c>) the anti-cheat replay measures
        /// against. Delivered with the player (its class is fixed) and carried as the distribution
        /// (base + per-level), not a resolved amount, so a client-side level-up rescales it correctly.
        /// </summary>
        public required List<AttributeDistribution> LockedBaseDistribution { get; set; }

        /// <summary>
        /// The player's class signature passive — the durable combat-identity bonus (flat or attribute-scaled)
        /// the live frontend battler composes into its attributes so they match the backend snapshot
        /// (<c>BattleSnapshot.ToBattler</c>) the anti-cheat replay measures against (spike #1126 area E).
        /// Delivered with the player (its class is fixed) alongside the locked-base fingerprint.
        /// </summary>
        public required SignaturePassive SignaturePassive { get; set; }

        /// <summary>
        /// The player's live combat-rating capability measure (<see cref="Game.Core.Battle.CombatRating.Rate"/>,
        /// spike #1526 Decision 7) — a numeric companion to the attributes screen's inert-stat signaling
        /// (#1528). Recomputed fresh from current state (not a stored battle snapshot), display-only, never
        /// recomputed client-side (no parity surface).
        /// </summary>
        public double PlayerRating { get; set; }

        public static PlayerData FromPlayer(
            CorePlayer player,
            IReadOnlyList<AttributeDistribution> lockedBaseDistribution,
            SignaturePassive signaturePassive,
            double playerRating)
        {
            var inventory = player.Inventory;

            // Precompute the loadout-order and equipped-slot lookups once, so the per-skill and
            // per-item projections below index them instead of rescanning SelectedSkills/EquipmentSlots
            // for every unlocked entry.
            var skillOrderById = player.SelectedSkills
                .Select((skill, order) => (skill.Id, order))
                .ToDictionary(entry => entry.Id, entry => entry.order);
            var equipSlotByItemId = inventory.EquipmentSlots
                .Where(slot => slot.ItemId.HasValue)
                .ToDictionary(slot => slot.ItemId.GetValueOrDefault());

            return new PlayerData
            {
                Id = player.Id,
                Name = player.Name,
                Level = player.Level,
                Exp = player.Exp,
                CurrentZone = player.CurrentZoneId,
                StatPointsGained = player.StatPoints.StatPointsGained,
                StatPointsUsed = player.StatPoints.StatPointsUsed,
                // Project every unlocked skill with its loadout state, parallel to UnlockedItems'
                // Equipped/EquipmentSlotId. SelectedSkills is already a deterministic (Order, SkillId)
                // ordering (PlayerCacheMapper.ToCore), so the equipped skill's index is its loadout order;
                // unselected skills report a null order.
                UnlockedSkills = player.Skills
                    .Select(skill =>
                    {
                        var selected = skillOrderById.TryGetValue(skill.Id, out var order);
                        return new UnlockedSkill
                        {
                            SkillId = skill.Id,
                            Selected = selected,
                            Order = selected ? order : null,
                        };
                    })
                    .ToList(),
                Attributes = player.StatPoints.StatAllocations
                    .Select(a => new BattlerAttribute
                    {
                        AttributeId = a.Attribute,
                        Amount = (decimal)a.Amount,
                    })
                    .ToList(),
                LogPreferences = player.LogPreferences
                    .Select(lp => new LogPreference
                    {
                        Id = lp.LogType,
                        Enabled = lp.Enabled,
                    })
                    .ToList(),
                InventoryData = new InventoryData
                {
                    UnlockedItems = inventory.UnlockedItems
                        .Select(slot =>
                        {
                            equipSlotByItemId.TryGetValue(slot.ItemId, out var equipSlot);

                            return new InventoryItem
                            {
                                ItemId = slot.ItemId,
                                Equipped = equipSlot is not null,
                                EquipmentSlotId = equipSlot is not null ? (int)equipSlot.Value : null,
                                Favorite = slot.Favorite,
                                AppliedMods = slot.AppliedMods
                                    .Select(am => new AppliedModModel
                                    {
                                        ItemModId = am.ItemModId,
                                        ItemModSlotId = am.ItemModSlotId,
                                    })
                                    .ToList(),
                            };
                        })
                        .ToList(),
                    UnlockedMods = inventory.UnlockedMods.ToList(),
                },
                LockedBaseDistribution = lockedBaseDistribution.ToList(),
                SignaturePassive = signaturePassive,
                PlayerRating = playerRating,
            };
        }
    }
}
