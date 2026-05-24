using Game.Core.Players;
using Game.Core.Players.Inventories;
using EntityPlayer = Game.Abstractions.Entities.Player;

namespace Game.DataAccess.Mapping
{
    internal static class PlayerMapper
    {
        /// <summary>
        /// Maps an entity <see cref="EntityPlayer"/> (with navigation properties loaded) to a
        /// domain <see cref="Player"/>.  Skills and items are mapped from the navigation
        /// properties that must already be included in the EF Core query.
        /// </summary>
        public static Player ToCore(EntityPlayer entity)
        {
            // Stat point allocations
            var statAllocations = (entity.PlayerAttributes ?? [])
                .Select(pa => new StatAllocation
                {
                    Attribute = (Core.EAttribute)pa.AttributeId,
                    Amount = (double)pa.Amount,
                }).ToList();

            // Build inventory
            var inventory = new Inventory();

            foreach (var ii in entity.InventoryItems ?? [])
            {
                if (ii.Item is null) continue;

                var coreItem = ItemMapper.ToCore(ii.Item, ii.InventoryItemMods);

                if (!ii.Equipped)
                {
                    inventory.InventorySlots.Add(new InventorySlot
                    {
                        InventoryItemId = ii.Id,
                        SlotNumber = ii.InventorySlotNumber,
                        Item = coreItem,
                    });
                }
                else
                {
                    var eSlot = inventory.EquipmentSlots
                        .FirstOrDefault(s => (int)s.Value == ii.InventorySlotNumber);
                    if (eSlot is not null)
                    {
                        eSlot.Item = coreItem;
                        eSlot.InventoryItemId = ii.Id;
                    }
                }
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
            };
        }
    }
}
