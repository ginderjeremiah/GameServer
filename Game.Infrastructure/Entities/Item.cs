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

        /// <summary>The <see cref="Core.EDamageType"/> weapon-leaf type this weapon deals, stored as its enum
        /// value, or null for a non-weapon item. Only meaningful on a <see cref="Core.EItemCategory.Weapon"/>
        /// item; the admin save requires a weapon to declare both this and a <see cref="GrantedSkillId"/>.</summary>
        public int? WeaponType { get; set; }

        /// <summary>The proficiency that gates equipping this item, or null when the item is ungated. A
        /// navigation-less optional FK, like the zone unlock challenge. The player must have reached
        /// <see cref="RequiredProficiencyLevel"/> in this proficiency to equip the item.</summary>
        public int? RequiredProficiencyId { get; set; }

        /// <summary>The minimum level the player must have reached in <see cref="RequiredProficiencyId"/> to
        /// equip this item. Only meaningful when <see cref="RequiredProficiencyId"/> is set.</summary>
        public int RequiredProficiencyLevel { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

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
