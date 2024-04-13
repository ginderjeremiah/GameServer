using DataAccess.Entities.ItemMods;
using DataAccess.Entities.Items;
using DataAccess.Entities.Skills;
using GameLibrary;
using GameServer.Models.Attributes;
using GameServer.Models.InventoryItems;
using GameServer.Models.Player;
using static GameServer.AttributeType;

namespace GameServer.BattleSimulation
{
    public class BattlePlayer : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattlePlayer(PlayerData playerData, List<Skill> skills, List<Item> items, List<ItemMod> itemMods)
        {
            var itemAttributes = playerData.InventoryData.Equipped
                .SelectNotNull(item => item)
                .SelectMany(item => GetItemAttributes(item, items, itemMods));

            Attributes = new BattleAttributes(playerData.Attributes.Concat(itemAttributes));
            CurrentHealth = Attributes[MaxHealth];
            Skills = playerData.SelectedSkills.Select(id => new BattleSkill(skills[id])).ToList();
            Level = playerData.Level;
        }

        private static IEnumerable<BattlerAttribute> GetItemAttributes(InventoryItem item, List<Item> items, List<ItemMod> itemMods)
        {
            return item.ItemMods
                .SelectMany(mod => itemMods[mod.ItemModId].Attributes.Select(att => new BattlerAttribute(att)))
                .Concat(items[item.ItemId].Attributes.Select(att => new BattlerAttribute(att)));
        }
    }
}
