using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for an item in the reference-data catalogue.</summary>
    public class Item : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Description { get; set; }
        public EItemCategory ItemCategoryId { get; set; }
        public ERarity RarityId { get; set; }
        public required string IconPath { get; set; }
        public required IEnumerable<BattlerAttribute> Attributes { get; set; }
        public required IEnumerable<ItemModSlot> ModSlots { get; set; }
        public required IEnumerable<int> Tags { get; set; }

        /// <summary>The id of the skill this item grants while equipped (authoring intent: the skill must be
        /// <see cref="ESkillAcquisition.Item"/>-flagged, enforced on save), or null for none.</summary>
        public int? GrantedSkillId { get; set; }

        /// <summary>The weapon-leaf <see cref="EDamageType"/> this weapon deals, or null for a non-weapon item.
        /// Only meaningful on a <see cref="EItemCategory.Weapon"/> item; the equipped weapon's type is what a
        /// weapon-typed skill matches against. Constrained to a weapon leaf, and required (alongside
        /// <see cref="GrantedSkillId"/>) for a weapon, by admin authoring validation.</summary>
        public EDamageType? WeaponType { get; set; }

        /// <summary>The id of the proficiency that gates equipping this item, or null when the item is
        /// ungated. Equipping requires the player to have reached <see cref="RequiredProficiencyLevel"/>
        /// in this proficiency (enforced server-side at equip time as anti-cheat).</summary>
        public int? RequiredProficiencyId { get; set; }

        /// <summary>The minimum level required in <see cref="RequiredProficiencyId"/> to equip this item.
        /// Only meaningful when <see cref="RequiredProficiencyId"/> is set.</summary>
        public int RequiredProficiencyLevel { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
