using GameCore.Entities;
using GameCore.Sessions;
using static GameCore.EAttribute;

namespace GameCore.BattleSimulation
{
    public class BattlePlayer : Battler
    {
        public override BattleAttributes Attributes { get; set; }
        public override double CurrentHealth { get; set; }
        public override List<BattleSkill> Skills { get; set; }
        public override int Level { get; set; }

        public BattlePlayer(Session session)
        {
            var itemAttributes = session.InventoryData.Equipped
                .SelectNotNull(item => item)
                .SelectMany(item => GetItemAttributes(item));

            Attributes = new BattleAttributes(session.BattlerAttributes.Concat(itemAttributes));
            CurrentHealth = Attributes[MaxHealth];
            Skills = session.Player.SelectedSkills.Select(skill => new BattleSkill(skill.Skill)).ToList();
            Level = session.Player.Level;
        }

        private static IEnumerable<BattlerAttribute> GetItemAttributes(InventoryItem item)
        {
            return item.Item.ItemAttributes.Select(att => new BattlerAttribute(att))
                .Concat(item.InventoryItemMods
                    .SelectMany(mod => mod.ItemMod.ItemModAttributes.
                        Select(att => new BattlerAttribute(att))
                    )
                );
        }
    }
}
