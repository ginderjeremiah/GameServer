namespace Game.Infrastructure.Entities
{
    /// <summary>
    /// A skill-synthesis recipe (spike #1125): authored reference data with a zero-based identity that combines
    /// owned <see cref="Inputs"/> skills (under optional proficiency <see cref="Conditions"/>) into one
    /// <see cref="ResultSkill"/>. Inputs/conditions are child collections so the
    /// <c>{ inputs, conditions, outputs }</c> shape generalizes later; V1 produces a single result skill. The
    /// execution (the <c>SynthesizeSkill</c> command) lands in a later sub-issue; this entity carries only the
    /// authored definition.
    /// </summary>
    public class SkillRecipe : IZeroBasedIdentityEntity
    {
        public int Id { get; set; }

        /// <summary>The skill this recipe produces (authored <see cref="Core.ESkillAcquisition.Synthesis"/>-flagged).</summary>
        public int ResultSkillId { get; set; }

        /// <summary>When set, the record is <em>retired</em> (see <see cref="Item.RetiredAt"/>).</summary>
        public DateTime? RetiredAt { get; set; }

        public virtual Skill ResultSkill { get => field ?? throw new NotLoadedException(nameof(ResultSkill)); set; }

        public virtual List<SkillRecipeInput> Inputs { get => field ?? throw new NotLoadedException(nameof(Inputs)); set; }
        public virtual List<SkillRecipeCondition> Conditions { get => field ?? throw new NotLoadedException(nameof(Conditions)); set; }
    }
}
