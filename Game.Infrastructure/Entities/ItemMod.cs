namespace Game.Infrastructure.Entities
{
    public class ItemMod : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int ItemModTypeId { get; set; }
        public int RarityId { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual ItemModType ItemModType { get => field ?? throw new NotLoadedException(nameof(ItemModType)); set; }
        public virtual Rarity Rarity { get => field ?? throw new NotLoadedException(nameof(Rarity)); set; }

        public virtual List<ItemModAttribute> ItemModAttributes { get => field ?? throw new NotLoadedException(nameof(ItemModAttributes)); set; }
        public virtual List<Tag> Tags { get => field ?? throw new NotLoadedException(nameof(Tags)); set; }
        public virtual List<UnlockedMod> UnlockedMods { get => field ?? throw new NotLoadedException(nameof(UnlockedMods)); set; }
    }
}
