using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a skill in the reference-data catalogue.</summary>
    public class Skill : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal BaseDamage { get; set; }
        public required IEnumerable<AttributeMultiplier> DamageMultipliers { get; set; }
        public required IEnumerable<SkillEffect> Effects { get; set; }
        public required string Description { get; set; }
        public int CooldownMs { get; set; }
        public required string IconPath { get; set; }
        public ERarity RarityId { get; set; }

        /// <summary>The leaf damage type this skill's direct hits deal (spike #1320); see
        /// <see cref="Core.Skills.Skill.DamageType"/>. Backfills to <see cref="EDamageType.Physical"/>.</summary>
        public EDamageType DamageType { get; set; }

        /// <summary>The skill's Aetheric conlang "word of power" (romanization rendered as glyphs, e.g.
        /// <c>sijren</c>), its <see cref="Pronunciation"/>, and its <see cref="Translation"/> — the decipher
        /// strings the Synthesis screen surfaces as a synthesized skill's identity (the same conlang the
        /// proficiency Lexicon uses). Display/authoring metadata; the battle never reads it.</summary>
        public required string Word { get; set; }
        public required string Pronunciation { get; set; }
        public required string Translation { get; set; }

        /// <summary>The channels allowed to grant this skill (authoring intent; see <see cref="ESkillAcquisition"/>).</summary>
        public ESkillAcquisition Acquisition { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
