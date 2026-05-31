using Game.Core.Players;
using Game.Core.Players.Inventories;
using EntityPlayer = Game.Abstractions.Entities.Player;

namespace Game.DataAccess.Mapping
{
    internal static class PlayerMapper
    {
        public static Player ToCore(EntityPlayer entity)
        {
            var statAllocations = (entity.PlayerAttributes ?? [])
                .Select(pa => new StatAllocation
                {
                    Attribute = (Core.EAttribute)pa.AttributeId,
                    Amount = (double)pa.Amount,
                }).ToList();

            var inventory = new Inventory();

            // Map unlocked items with their applied mods
            var appliedModsByItem = (entity.AppliedMods ?? [])
                .GroupBy(am => am.ItemId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var ui in entity.UnlockedItems ?? [])
            {
                if (ui.Item is null) continue;

                var coreItem = ItemMapper.ToCore(ui.Item);
                var appliedMods = new List<AppliedModSlot>();

                if (appliedModsByItem.TryGetValue(ui.ItemId, out var mods))
                {
                    foreach (var am in mods)
                    {
                        if (am.ItemMod is null) continue;
                        appliedMods.Add(new AppliedModSlot
                        {
                            ItemModId = am.ItemModId,
                            ItemModSlotId = am.ItemModSlotId,
                            ItemMod = ItemMapper.ModToCore(am.ItemMod),
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
            foreach (var um in entity.UnlockedMods ?? [])
            {
                inventory.UnlockedMods.Add(um.ItemModId);
            }

            // Map player skills
            var playerSkills = (entity.PlayerSkills ?? [])
                .Where(ps => ps.Skill is not null)
                .ToList();

            var skills = playerSkills
                .Select(ps => SkillMapper.ToCore(ps.Skill))
                .ToList();

            var selectedSkills = playerSkills
                .Where(ps => ps.Selected)
                .Select(ps => SkillMapper.ToCore(ps.Skill))
                .ToList();

            var logPreferences = (entity.LogPreferences ?? [])
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
                Skills = skills,
                SelectedSkills = selectedSkills,
                LogPreferences = logPreferences,
            };
        }
    }
}
