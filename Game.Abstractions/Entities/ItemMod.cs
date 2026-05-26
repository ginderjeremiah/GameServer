namespace Game.Abstractions.Entities
{
    public partial class ItemMod : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public bool Removable { get; set; }
        public required string Description { get; set; }
        public int ItemModTypeId { get; set; }

        public virtual List<ItemModAttribute> ItemModAttributes { get => field ?? throw new NavigationNotLoadedException(nameof(ItemModAttributes)); set; }
        public virtual ItemModType ItemModType { get => field ?? throw new NavigationNotLoadedException(nameof(ItemModType)); set; }
        public virtual List<Tag> Tags { get => field ?? throw new NavigationNotLoadedException(nameof(Tags)); set; }
        public virtual List<UnlockedMod> UnlockedMods { get => field ?? throw new NavigationNotLoadedException(nameof(UnlockedMods)); set; }
    }
}
