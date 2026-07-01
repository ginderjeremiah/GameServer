namespace Game.Infrastructure.Entities
{
    public class Skill : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal BaseDamage { get; set; }
        public required string Description { get; set; }
        public int CooldownMs { get; set; }
        public required string IconPath { get; set; }
        public int RarityId { get; set; }

        /// <summary>The skill's Aetheric conlang "word of power" — the romanization rendered as glyphs
        /// (e.g. <c>sijren</c>). Decorative flavour (the same conlang the proficiency Lexicon uses), surfaced
        /// most prominently on the Synthesis screen as a synthesized skill's discovered identity:
        /// <see cref="Pronunciation"/> and <see cref="Translation"/> are its romanized pronunciation and
        /// meaning. Display metadata only — the battle never reads it, so it lives on the entity + contract
        /// but not the lean <see cref="Core.Skills.Skill"/> model (mirroring the proficiency word of power).</summary>
        public required string Word { get; set; }

        /// <summary>The pronunciation of the <see cref="Word"/> (e.g. <c>sij·ren</c>).</summary>
        public required string Pronunciation { get; set; }

        /// <summary>The translation of the <see cref="Word"/> (e.g. <c>The Riven Frost</c>).</summary>
        public required string Translation { get; set; }

        /// <summary>The <see cref="Core.ESkillAcquisition"/> bitmask of channels allowed to grant this skill.</summary>
        public int Acquisition { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual Rarity Rarity { get => field ?? throw new NotLoadedException(nameof(Rarity)); set; }

        public virtual List<SkillDamagePortion> SkillDamagePortions { get => field ?? throw new NotLoadedException(nameof(SkillDamagePortions)); set; }
        public virtual List<SkillDamageMultiplier> SkillDamageMultipliers { get => field ?? throw new NotLoadedException(nameof(SkillDamageMultipliers)); set; }
        public virtual List<SkillEffect> SkillEffects { get => field ?? throw new NotLoadedException(nameof(SkillEffects)); set; }
        public virtual List<EnemySkill> EnemySkills { get => field ?? throw new NotLoadedException(nameof(EnemySkills)); set; }
        public virtual List<PlayerSkill> PlayerSkills { get => field ?? throw new NotLoadedException(nameof(PlayerSkills)); set; }
    }
}
