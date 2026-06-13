using Game.Abstractions.Contracts;
using Game.Api.Models.InventoryItems;
using CorePlayer = Game.Core.Players.Player;

namespace Game.Api.Models.Player
{
    public class PlayerData : IModel
    {
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

        public static PlayerData FromPlayer(CorePlayer player)
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
                Name = player.Name,
                Level = player.Level,
                Exp = player.Exp,
                CurrentZone = player.CurrentZoneId,
                StatPointsGained = player.StatPoints.StatPointsGained,
                StatPointsUsed = player.StatPoints.StatPointsUsed,
                // Project every unlocked skill with its loadout state, parallel to UnlockedItems'
                // Equipped/EquipmentSlotId. SelectedSkills is already a deterministic (Order, SkillId)
                // ordering (PlayerMapper.ToCore), so the equipped skill's index is its loadout order;
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
            };
        }
    }
}
