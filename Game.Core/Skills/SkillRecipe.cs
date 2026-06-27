namespace Game.Core.Skills
{
    /// <summary>
    /// A skill-synthesis recipe (spike #1125): the authored, non-consumptive transformation that combines a set
    /// of owned input skills (under optional proficiency-level conditions) into a single result skill. Static,
    /// zero-based reference data shared from the immutable cache; the lean domain view the synthesis command
    /// validates against. Synthesis is deterministic and possession-gated — never resource-consuming — so the
    /// recipe carries no cost: owning the inputs and meeting the conditions is the cost.
    /// </summary>
    public class SkillRecipe
    {
        public required int Id { get; init; }

        /// <summary>The skill this recipe produces. The skill is authored as <see cref="ESkillAcquisition.Synthesis"/>-flagged.</summary>
        public required int ResultSkillId { get; init; }

        /// <summary>The input skills that must all be owned (as unlocked skills) to synthesize the result. Always ≥ 1.</summary>
        public required IReadOnlyList<int> InputSkillIds { get; init; }

        /// <summary>The proficiency-level gates that must all be met to synthesize the result (may be empty).</summary>
        public required IReadOnlyList<RecipeCondition> Conditions { get; init; }

        /// <summary>When true the recipe is retired: it is no longer offered or hinted, though already-synthesized
        /// results persist. The synthesis command refuses a retired recipe.</summary>
        public required bool IsRetired { get; init; }
    }

    /// <summary>A proficiency-level gate on a recipe: <see cref="ProficiencyId"/> must be at least <see cref="MinLevel"/>.</summary>
    public readonly record struct RecipeCondition(int ProficiencyId, int MinLevel);
}
