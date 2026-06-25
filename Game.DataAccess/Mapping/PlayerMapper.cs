using Game.Abstractions.Contracts.Identity;
using Game.Core.Players;
using EntityLogPreference = Game.Infrastructure.Entities.LogPreference;
using EntityPlayer = Game.Infrastructure.Entities.Player;
using EntityPlayerAttribute = Game.Infrastructure.Entities.PlayerAttribute;
using EntityPlayerSkill = Game.Infrastructure.Entities.PlayerSkill;
using EntityUnlockedItem = Game.Infrastructure.Entities.UnlockedItem;
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
                ClassId = newPlayer.ClassId,
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

            // The class's starter equipment, unlocked and equipped. The equipped slot is stored on the
            // unlocked-item row itself (a non-null EquipmentSlotId), the same shape the player-load path reads
            // back to populate the inventory's equipment slots.
            player.UnlockedItems = newPlayer.Equipment
                .Select(equipment => new EntityUnlockedItem
                {
                    Player = player,
                    ItemId = equipment.ItemId,
                    EquipmentSlotId = (int)equipment.EquipmentSlot,
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
        /// Projects a materialized player entity to a lightweight <see cref="PlayerSummary"/>. The
        /// EF-translated <c>Select</c> projections in the Users repository (which must stay expressions for
        /// SQL translation) mirror these same fields — keep them in step.
        /// </summary>
        public static PlayerSummary ToSummary(EntityPlayer entity)
        {
            return new PlayerSummary
            {
                Id = entity.Id,
                Name = entity.Name,
                Level = entity.Level,
                CurrentZoneId = entity.CurrentZoneId,
                LastActivity = entity.LastActivity,
            };
        }
    }
}
