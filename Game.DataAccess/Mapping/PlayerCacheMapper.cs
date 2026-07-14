using Game.Abstractions.DataAccess;
using Game.Core.Players;
using Game.Core.Players.Inventories;

namespace Game.DataAccess.Mapping
{
    /// <summary>
    /// Maps the player aggregate to and from its lean <see cref="PlayerCacheModel"/>. <see cref="ToCacheModel"/>
    /// strips every owned reference down to ids for serialization; <see cref="ToCore"/> rehydrates the aggregate,
    /// re-resolving that reference data from the in-memory catalogs so a loaded player always carries current
    /// reference data rather than the snapshot it was persisted with. Both the Redis cache and the database read
    /// produce a <see cref="PlayerCacheModel"/>, so this is the single rehydration path for the aggregate (#1155).
    /// </summary>
    internal static class PlayerCacheMapper
    {
        /// <summary>
        /// Projects a live <see cref="Player"/> to its lean cache model, reducing every owned reference (items,
        /// item mods, skills) to ids while preserving the player-specific state (equip slots, favorites, applied
        /// mods, loadout order).
        /// </summary>
        public static PlayerCacheModel ToCacheModel(Player player)
        {
            // Equipment lives on a separate slot list in the domain but is stored against the unlocked item
            // (mirroring the relational table) in the lean model, so resolve each item's occupied slot up front.
            var equipmentSlotByItemId = new Dictionary<int, int>();
            foreach (var slot in player.Inventory.EquipmentSlots)
            {
                if (slot.ItemId is int equippedItemId)
                {
                    equipmentSlotByItemId[equippedItemId] = (int)slot.Value;
                }
            }

            var unlockedItems = player.Inventory.UnlockedItems
                .Select(ui => new CachedUnlockedItem
                {
                    ItemId = ui.ItemId,
                    EquipmentSlotId = equipmentSlotByItemId.TryGetValue(ui.ItemId, out var slotId) ? slotId : null,
                    Favorite = ui.Favorite,
                })
                .ToList();

            var appliedMods = player.Inventory.UnlockedItems
                .SelectMany(ui => ui.AppliedMods.Select(am => new CachedAppliedMod
                {
                    ItemId = ui.ItemId,
                    ItemModId = am.ItemModId,
                    ItemModSlotId = am.ItemModSlotId,
                }))
                .ToList();

            // The equipped order is implicit in the ordered SelectedSkills list; capture it as a per-skill flag
            // (Selected) plus an Order index so the model mirrors the relational shape and round-trips.
            var orderBySelectedSkillId = new Dictionary<int, int>();
            for (var i = 0; i < player.SelectedSkills.Count; i++)
            {
                orderBySelectedSkillId[player.SelectedSkills[i].Id] = i;
            }

            var skills = player.Skills
                .Select(s => new CachedPlayerSkill
                {
                    SkillId = s.Id,
                    Selected = orderBySelectedSkillId.TryGetValue(s.Id, out var order),
                    Order = order,
                })
                .ToList();

            return new PlayerCacheModel
            {
                Id = player.Id,
                ClassId = player.ClassId,
                Name = player.Name,
                Level = player.Level,
                Exp = player.Exp,
                CurrentZoneId = player.CurrentZoneId,
                LastActivity = player.LastActivity,
                AutoChallengeBoss = player.AutoChallengeBoss,
                LastCreditedBattleSeed = player.LastCreditedBattleSeed,
                StatPointsGained = player.StatPoints.StatPointsGained,
                StatPointsUsed = player.StatPoints.StatPointsUsed,
                StatAllocations = player.StatPoints.StatAllocations,
                UnlockedItems = unlockedItems,
                AppliedMods = appliedMods,
                UnlockedModIds = player.Inventory.UnlockedMods.ToList(),
                Skills = skills,
                LogPreferences = player.LogPreferences,
                Lessons = player.Lessons,
            };
        }

        /// <summary>
        /// Rehydrates a <see cref="Player"/> from its lean model, resolving every owned reference (items, item
        /// mods, skills) from the in-memory cached catalogs rather than from the persisted snapshot. An owned
        /// reference that no longer resolves fails the load loudly via <see cref="ResolveOrThrow"/> rather than
        /// being silently dropped.
        /// </summary>
        public static Player ToCore(PlayerCacheModel model, IItems items, IItemMods itemMods, ISkills skills)
        {
            var inventory = new Inventory();

            var appliedModsByItem = model.AppliedMods
                .GroupBy(am => am.ItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var unlockedItems = new List<UnlockedItemSlot>();
            foreach (var ui in model.UnlockedItems)
            {
                var coreItem = ResolveOrThrow(items.GetItem, ui.ItemId, model.Id, "item");
                var appliedMods = new List<AppliedModSlot>();

                if (appliedModsByItem.TryGetValue(ui.ItemId, out var mods))
                {
                    foreach (var am in mods)
                    {
                        appliedMods.Add(new AppliedModSlot
                        {
                            ItemModId = am.ItemModId,
                            ItemModSlotId = am.ItemModSlotId,
                            ItemMod = ResolveOrThrow(itemMods.GetItemMod, am.ItemModId, model.Id, "item mod"),
                        });
                    }
                }

                unlockedItems.Add(new UnlockedItemSlot
                {
                    Item = coreItem,
                    AppliedMods = appliedMods,
                    Favorite = ui.Favorite,
                });

                if (ui.EquipmentSlotId.HasValue)
                {
                    var eSlot = inventory.EquipmentSlots
                        .FirstOrDefault(s => (int)s.Value == ui.EquipmentSlotId.Value);
                    eSlot?.Set(coreItem);
                }
            }

            // Assign through the setter so the inventory builds its id-keyed index once.
            inventory.UnlockedItems = unlockedItems;

            foreach (var modId in model.UnlockedModIds)
            {
                inventory.UnlockedMods.Add(modId);
            }

            // Resolve each unlocked skill once and share the instances with the equipped set. Order the equipped
            // set by (Order, SkillId) for a stable total order: the equipped order is captured into
            // BattleSnapshot.SkillIds and sent to the client, so it must be deterministic to preserve battle
            // parity — including for legacy rows that default to Order = 0 (SkillId is the deterministic tie-break).
            var skillsById = model.Skills
                .ToDictionary(ps => ps.SkillId, ps => ResolveOrThrow(skills.GetSkill, ps.SkillId, model.Id, "skill"));

            var playerSkills = skillsById.Values.ToList();

            var selectedSkills = model.Skills
                .Where(ps => ps.Selected)
                .OrderBy(ps => ps.Order)
                .ThenBy(ps => ps.SkillId)
                .Select(ps => skillsById[ps.SkillId])
                .ToList();

            return new Player
            {
                Id = model.Id,
                ClassId = model.ClassId,
                Name = model.Name,
                Level = model.Level,
                Exp = model.Exp,
                CurrentZoneId = model.CurrentZoneId,
                LastActivity = model.LastActivity,
                AutoChallengeBoss = model.AutoChallengeBoss,
                LastCreditedBattleSeed = model.LastCreditedBattleSeed,
                StatPoints = new PlayerStatPoints
                {
                    StatAllocations = model.StatAllocations,
                    StatPointsGained = model.StatPointsGained,
                    StatPointsUsed = model.StatPointsUsed,
                },
                Inventory = inventory,
                Skills = playerSkills,
                SelectedSkills = selectedSkills,
                LogPreferences = model.LogPreferences,
                Lessons = model.Lessons,
            };
        }

        /// <summary>
        /// Resolves an owned reference (item / item mod / skill) against its catalog, rethrowing a catalog miss as a
        /// loud, diagnosable <see cref="OrphanedReferenceException"/> that names the player, catalog, and missing
        /// id. We never silently drop the player-owned row; instead the aggregate fails loudly so a content-data
        /// mistake (a removed referenced id) is obvious from logs rather than surfacing as an opaque load failure.
        /// </summary>
        private static T ResolveOrThrow<T>(Func<int, T> resolve, int referenceId, int playerId, string catalogName)
        {
            try
            {
                return resolve(referenceId);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // Only a catalog miss (GetById's documented out-of-range contract) is an orphaned reference;
                // any other failure propagates unwrapped so the orphaned-reference diagnosis stays truthful.
                throw new OrphanedReferenceException(playerId, catalogName, referenceId, ex);
            }
        }
    }
}
