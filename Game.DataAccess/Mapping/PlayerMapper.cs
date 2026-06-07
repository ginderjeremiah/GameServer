using Game.Abstractions.DataAccess;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using EntityPlayer = Game.Abstractions.Entities.Player;

namespace Game.DataAccess.Mapping
{
    internal static class PlayerMapper
    {
        /// <summary>
        /// Maps a player entity (carrying only player-specific relational data) to a domain
        /// <see cref="Player"/>, resolving the reference-data portion (items, item mods, skills)
        /// from the in-memory cached catalogs rather than from EF-loaded navigation properties.
        /// </summary>
        public static Player ToCore(EntityPlayer entity, IItems items, IItemMods itemMods, ISkills skills)
        {
            var statAllocations = entity.PlayerAttributes
                .Select(pa => new StatAllocation
                {
                    Attribute = (Core.EAttribute)pa.AttributeId,
                    Amount = (double)pa.Amount,
                }).ToList();

            var inventory = new Inventory();

            // Map unlocked items with their applied mods
            var appliedModsByItem = entity.AppliedMods
                .GroupBy(am => am.ItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var ui in entity.UnlockedItems)
            {
                var coreItem = items.GetItem(ui.ItemId);
                var appliedMods = new List<AppliedModSlot>();

                if (appliedModsByItem.TryGetValue(ui.ItemId, out var mods))
                {
                    foreach (var am in mods)
                    {
                        appliedMods.Add(new AppliedModSlot
                        {
                            ItemModId = am.ItemModId,
                            ItemModSlotId = am.ItemModSlotId,
                            ItemMod = itemMods.GetItemMod(am.ItemModId),
                        });
                    }
                }

                var slot = new UnlockedItemSlot
                {
                    ItemId = ui.ItemId,
                    Item = coreItem,
                    AppliedMods = appliedMods,
                };

                inventory.UnlockedItems.Add(slot);

                if (ui.EquipmentSlotId.HasValue)
                {
                    var eSlot = inventory.EquipmentSlots
                        .FirstOrDefault(s => (int)s.Value == ui.EquipmentSlotId.Value);
                    if (eSlot is not null)
                    {
                        eSlot.Item = coreItem;
                        eSlot.ItemId = ui.ItemId;
                    }
                }
            }

            // Map unlocked mods
            foreach (var um in entity.UnlockedMods)
            {
                inventory.UnlockedMods.Add(um.ItemModId);
            }

            // Map player skills, resolving each skill from the cached catalog by id
            var skillsById = entity.PlayerSkills
                .ToDictionary(ps => ps.SkillId, ps => skills.GetSkill(ps.SkillId));

            var playerSkills = skillsById.Values.ToList();

            var selectedSkills = entity.PlayerSkills
                .Where(ps => ps.Selected)
                .Select(ps => skillsById[ps.SkillId])
                .ToList();

            var logPreferences = entity.LogPreferences
                .Select(lp => new Core.Players.LogPreference
                {
                    LogType = (Core.ELogType)lp.LogTypeId,
                    Enabled = lp.Enabled,
                }).ToList();

            return new Player
            {
                Id = entity.Id,
                Name = entity.Name,
                Level = entity.Level,
                Exp = entity.Exp,
                CurrentZoneId = entity.CurrentZoneId,
                StatPoints = new PlayerStatPoints(statAllocations)
                {
                    StatPointsGained = entity.StatPointsGained,
                    StatPointsUsed = entity.StatPointsUsed,
                },
                Inventory = inventory,
                Skills = playerSkills,
                SelectedSkills = selectedSkills,
                LogPreferences = logPreferences,
            };
        }
    }
}
