using Game.Abstractions.DataAccess;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using EntityLogPreference = Game.Abstractions.Entities.LogPreference;
using EntityPlayer = Game.Abstractions.Entities.Player;
using EntityPlayerAttribute = Game.Abstractions.Entities.PlayerAttribute;
using EntityPlayerSkill = Game.Abstractions.Entities.PlayerSkill;
using EntityUser = Game.Abstractions.Entities.User;

namespace Game.DataAccess.Mapping
{
    internal static class PlayerMapper
    {
        /// <summary>
        /// Builds the persisted entity graph for a brand-new player from its domain
        /// <see cref="NewPlayer"/> blueprint, linking it to the owning <paramref name="user"/> via the
        /// navigation property (so EF resolves the foreign key without the user's store-generated id).
        /// </summary>
        public static EntityPlayer ToEntity(NewPlayer newPlayer, EntityUser user)
        {
            var player = new EntityPlayer
            {
                User = user,
                Name = newPlayer.Name,
                Level = newPlayer.Level,
                Exp = newPlayer.Exp,
                CurrentZoneId = newPlayer.CurrentZoneId,
                StatPointsGained = newPlayer.StatPointsGained,
                StatPointsUsed = newPlayer.StatPointsUsed,
            };

            player.PlayerSkills = newPlayer.Skills
                .Select(skill => new EntityPlayerSkill
                {
                    Player = player,
                    SkillId = skill.SkillId,
                    Selected = skill.Selected,
                }).ToList();

            player.PlayerAttributes = newPlayer.Attributes
                .Select(attribute => new EntityPlayerAttribute
                {
                    Player = player,
                    AttributeId = (int)attribute.Attribute,
                    Amount = (decimal)attribute.Amount,
                }).ToList();

            player.LogPreferences = newPlayer.LogPreferences
                .Select(preference => new EntityLogPreference
                {
                    Player = player,
                    LogTypeId = (int)preference.LogType,
                    Enabled = preference.Enabled,
                }).ToList();

            return player;
        }

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
