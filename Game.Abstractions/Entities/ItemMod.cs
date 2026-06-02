namespace Game.Abstractions.Entities
{
    public class ItemMod : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int ItemModTypeId { get; set; }
        public int RarityId { get; set; }

        public virtual ItemModType ItemModType { get => field ?? throw new NotLoadedException(nameof(ItemModType)); set; }
        public virtual Rarity Rarity { get => field ?? throw new NotLoadedException(nameof(Rarity)); set; }

        public virtual List<ItemModAttribute> ItemModAttributes { get => field ?? throw new NotLoadedException(nameof(ItemModAttributes)); set; }
        public virtual List<Tag> Tags { get => field ?? throw new NotLoadedException(nameof(Tags)); set; }
        public virtual List<UnlockedMod> UnlockedMods { get => field ?? throw new NotLoadedException(nameof(UnlockedMods)); set; }
    }
}
