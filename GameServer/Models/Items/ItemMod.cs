using GameServer.Models.Attributes;

namespace GameServer.Models.Items
{
    public class ItemMod : IModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Removable { get; set; }
        public string Description { get; set; }
        public int SlotTypeId { get; set; }
        public List<BattlerAttribute> Attributes { get; set; }

        public ItemMod() { }

        public ItemMod(GameCore.Entities.ItemMod itemMod)
        {
            Id = itemMod.Id;
            Name = itemMod.Name;
            Removable = itemMod.Removable;
            Description = itemMod.Description;
            SlotTypeId = itemMod.SlotTypeId;
            Attributes = itemMod.ItemModAttributes.Select(a => new BattlerAttribute(a)).ToList();
        }
    }
}
