using Game.Core;

namespace Game.Abstractions.Contracts
{
    /// <summary>Read contract for a skill in the reference-data catalogue.</summary>
    public class Skill : IModel
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal BaseDamage { get; set; }

        /// <summary>This skill's own base critical-hit chance (decimal probability, 0.05 = 5%), scaled by the
        /// attacking battler's <see cref="EAttribute.CriticalChanceMultiplier"/> at fire time — see
        /// <see cref="Core.Skills.Skill.CriticalChance"/>. Existing skills backfill to <c>0</c> (never crits).</summary>
        public decimal CriticalChance { get; set; }

        public required IEnumerable<AttributeMultiplier> DamageMultipliers { get; set; }
        public required IEnumerable<SkillEffect> Effects { get; set; }

        /// <summary>The weighted leaf-type split this skill's direct hits deal (spike #1343); see
        /// <see cref="Core.Skills.Skill.DamagePortions"/>. Every skill carries at least one portion (existing
        /// skills backfilled to <c>[{ Physical, 1.0 }]</c>).</summary>
        public required IEnumerable<SkillDamagePortion> DamagePortions { get; set; }

        public required string Description { get; set; }
        public int CooldownMs { get; set; }
        public required string IconPath { get; set; }
        public ERarity RarityId { get; set; }

        /// <summary>The skill's Aetheric conlang "word of power" (romanization rendered as glyphs, e.g.
        /// <c>sijren</c>), its <see cref="Pronunciation"/>, and its <see cref="Translation"/> — the decipher
        /// strings the Synthesis screen surfaces as a synthesized skill's identity (the same conlang the
        /// proficiency Lexicon uses). Display/authoring metadata; the battle never reads it.</summary>
        public required string Word { get; set; }
        public required string Pronunciation { get; set; }
        public required string Translation { get; set; }

        /// <summary>The channels allowed to grant this skill (authoring intent; see <see cref="ESkillAcquisition"/>).</summary>
        public ESkillAcquisition Acquisition { get; set; }

        /// <summary>Authoring-only design rationale (why this piece exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).
        /// Null while active.</summary>
        public DateTime? RetiredAt { get; set; }
    }
}
