namespace Game.Abstractions.Contracts
{
    /// <summary>Read/authoring contract for a skill-synthesis recipe in the reference-data catalogue (spike
    /// #1125). The child collections are read projections; the identity save ignores them (they are persisted
    /// through the dedicated relationship setters, mirroring the proficiency editor).</summary>
    public class SkillRecipe : IModel
    {
        public int Id { get; set; }

        /// <summary>The skill this recipe produces (authoring intent: <see cref="ESkillAcquisition.Synthesis"/>-flagged).</summary>
        public int ResultSkillId { get; set; }

        /// <summary>Authoring-only design rationale (why this recipe exists) — surfaced in the Workbench and
        /// version-controlled via the content export. The battle never reads it and the client never renders it.</summary>
        public required string DesignerNotes { get; set; }

        /// <summary>When set, the record is retired (out of circulation but kept resolvable by id).</summary>
        public DateTime? RetiredAt { get; set; }

        /// <summary>The input skill ids the player must own (as unlocked skills) to synthesize the result.</summary>
        public required IEnumerable<int> InputSkillIds { get; set; }

        /// <summary>The proficiency-level gates that must all be met to synthesize the result.</summary>
        public required IEnumerable<SkillRecipeCondition> Conditions { get; set; }
    }
}
