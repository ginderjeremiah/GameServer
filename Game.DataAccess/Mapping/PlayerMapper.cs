using Game.Abstractions.DataAccess;
using Game.Core.Players;
using Game.Core.Players.Inventories;
using EntityLogPreference = Game.Infrastructure.Entities.LogPreference;
using EntityPlayer = Game.Infrastructure.Entities.Player;
using EntityPlayerAttribute = Game.Infrastructure.Entities.PlayerAttribute;
using EntityPlayerSkill = Game.Infrastructure.Entities.PlayerSkill;
using EntityUser = Game.Infrastructure.Entities.User;

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
                // Anchor away-time tracking at creation so a brand-new player's first login computes a fresh
                // (near-zero) away period rather than an enormous one from the default DateTime.
                LastActivity = DateTime.UtcNow,
            };

            player.PlayerSkills = newPlayer.Skills
                .Select(skill => new EntityPlayerSkill
                {
                    Player = player,
                    SkillId = skill.SkillId,
                    Selected = skill.Selected,
                    Order = skill.Order,
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

            var unlockedItems = new List<UnlockedItemSlot>();
            foreach (var ui in entity.UnlockedItems)
            {
                var coreItem = ResolveOrThrow(items.GetItem, ui.ItemId, entity.Id, "item");
                var appliedMods = new List<AppliedModSlot>();

                if (appliedModsByItem.TryGetValue(ui.ItemId, out var mods))
                {
                    foreach (var am in mods)
                    {
                        appliedMods.Add(new AppliedModSlot
                        {
                            ItemModId = am.ItemModId,
                            ItemModSlotId = am.ItemModSlotId,
                            ItemMod = ResolveOrThrow(itemMods.GetItemMod, am.ItemModId, entity.Id, "item mod"),
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
                    if (eSlot is not null)
                    {
                        eSlot.Set(coreItem);
                    }
                }
            }

            // Assign through the setter so the inventory builds its id-keyed index once.
            inventory.UnlockedItems = unlockedItems;

            // Map unlocked mods
            foreach (var um in entity.UnlockedMods)
            {
                inventory.UnlockedMods.Add(um.ItemModId);
            }

            // Map player skills, resolving each skill from the cached catalog by id
            var skillsById = entity.PlayerSkills
                .ToDictionary(ps => ps.SkillId, ps => ResolveOrThrow(skills.GetSkill, ps.SkillId, entity.Id, "skill"));

            var playerSkills = skillsById.Values.ToList();

            // Order the equipped set by (Order, SkillId) for a stable total order. The equipped
            // order is captured into BattleSnapshot.SkillIds and sent to the client, so the
            // backend's order must be deterministic to preserve battle parity — including for
            // legacy rows that default to Order = 0 (SkillId is the deterministic tie-break).
            var selectedSkills = entity.PlayerSkills
                .Where(ps => ps.Selected)
                .OrderBy(ps => ps.Order)
                .ThenBy(ps => ps.SkillId)
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
                LastActivity = entity.LastActivity,
                AutoChallengeBoss = entity.AutoChallengeBoss,
                StatPoints = new PlayerStatPoints
                {
                    StatAllocations = statAllocations,
                    StatPointsGained = entity.StatPointsGained,
                    StatPointsUsed = entity.StatPointsUsed,
                },
                Inventory = inventory,
                Skills = playerSkills,
                SelectedSkills = selectedSkills,
                LogPreferences = logPreferences,
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
