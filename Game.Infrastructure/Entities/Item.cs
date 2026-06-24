namespace Game.Infrastructure.Entities
{
    public class Item : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public int ItemCategoryId { get; set; }
        public required string IconPath { get; set; }
        public int RarityId { get; set; }

        /// <summary>The skill this item grants while equipped, or null for none. The id is the only
        /// persisted link (an optional FK).</summary>
        public int? GrantedSkillId { get; set; }

        /// <summary>When set, the record is <em>retired</em>: out of circulation for new acquisition but
        /// kept at its slot and resolvable by id so existing references stay valid. Null while active.</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual ItemCategory ItemCategory { get => field ?? throw new NotLoadedException(nameof(ItemCategory)); set; }
        public virtual Rarity Rarity { get => field ?? throw new NotLoadedException(nameof(Rarity)); set; }
        public virtual Skill? GrantedSkill { get; set; }

        public virtual List<ItemAttribute> ItemAttributes { get => field ?? throw new NotLoadedException(nameof(ItemAttributes)); set; }
        public virtual List<ItemModSlot> ItemModSlots { get => field ?? throw new NotLoadedException(nameof(ItemModSlots)); set; }
        public virtual List<Tag> Tags { get => field ?? throw new NotLoadedException(nameof(Tags)); set; }
        public virtual List<UnlockedItem> UnlockedItems { get => field ?? throw new NotLoadedException(nameof(UnlockedItems)); set; }
    }
}
