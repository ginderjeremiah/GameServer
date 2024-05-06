using GameCore.Entities.InventoryItems;
using GameCore.Entities.ItemMods;
using GameCore.Entities.Items;
using GameCore.Entities.Skills;
using GameCore.Sessions;
using static GameCore.BattleSimulation.AttributeType;

namespace GameCore.BattleSimulation
{
    public class BattlePlayer : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattlePlayer(Session session, List<Skill> skills, List<Item> items, List<ItemMod> itemMods)
        {
            var itemAttributes = session.InventoryData.Equipped
                .SelectNotNull(item => item)
                .SelectMany(item => GetItemAttributes(item, items, itemMods));

            Attributes = new BattleAttributes(session.BattlerAttributes.Concat(itemAttributes));
            CurrentHealth = Attributes[MaxHealth];
            Skills = session.Player.SelectedSkills.Select(id => new BattleSkill(skills[id])).ToList();
            Level = session.Player.Level;
        }

        private static IEnumerable<BattlerAttribute> GetItemAttributes(InventoryItem item, List<Item> items, List<ItemMod> itemMods)
        {
            return item.ItemMods
                .SelectMany(mod => itemMods[mod.ItemModId].Attributes.Select(att => new BattlerAttribute(att)))
                .Concat(items[item.ItemId].Attributes.Select(att => new BattlerAttribute(att)));
        }
    }
}
