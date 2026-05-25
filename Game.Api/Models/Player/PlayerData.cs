using Game.Api.Models.Attributes;
using Game.Api.Models.Common;
using Game.Api.Models.InventoryItems;
using CorePlayer = Game.Core.Players.Player;

namespace Game.Api.Models.Player
{
    public class PlayerData : IModel
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public int Exp { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }
        public List<int> SelectedSkills { get; set; }
        public int CurrentZone { get; set; }
        public int StatPointsGained { get; set; }
        public int StatPointsUsed { get; set; }
        public List<LogPreference> LogPreferences { get; set; }
        public InventoryData InventoryData { get; set; }

        public static PlayerData FromPlayer(CorePlayer player)
        {
            var inventory = player.Inventory;

            return new PlayerData
            {
                Name = player.Name,
                Level = player.Level,
                Exp = player.Exp,
                CurrentZone = player.CurrentZoneId,
                StatPointsGained = player.StatPoints.StatPointsGained,
                StatPointsUsed = player.StatPoints.StatPointsUsed,
                SelectedSkills = player.SelectedSkills.Select(s => s.Id).ToList(),
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
                            var equipSlot = inventory.EquipmentSlots
                                .FirstOrDefault(es => es.ItemId == slot.ItemId);

                            return new InventoryItem
                            {
                                ItemId = slot.ItemId,
                                Equipped = equipSlot is not null,
                                EquipmentSlotId = equipSlot is not null ? (int)equipSlot.Value : null,
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
