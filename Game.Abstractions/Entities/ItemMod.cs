namespace Game.Abstractions.Entities
{
    public partial class ItemMod : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool Removable { get; set; }
        public string Description { get; set; }
        public int ItemModTypeId { get; set; }

        public virtual List<ItemModAttribute> ItemModAttributes { get; set; }
        public virtual ItemModType ItemModType { get; set; }
        public virtual List<Tag> Tags { get; set; }
        public virtual List<UnlockedMod> UnlockedMods { get; set; }
    }
}
