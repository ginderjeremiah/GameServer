using Contracts = Game.Abstractions.Contracts;
using CoreRecipeCondition = Game.Core.Skills.RecipeCondition;
using CoreSkillRecipe = Game.Core.Skills.SkillRecipe;
using EntitySkillRecipe = Game.Infrastructure.Entities.SkillRecipe;
using EntitySkillRecipeInput = Game.Infrastructure.Entities.SkillRecipeInput;
using EntitySkillRecipeCondition = Game.Infrastructure.Entities.SkillRecipeCondition;

namespace Game.DataAccess.Mapping
{
    internal static class SkillRecipeMapper
    {
        /// <summary>Maps an entity <see cref="EntitySkillRecipe"/> (with its inputs and conditions loaded) to the
        /// read/authoring contract. Inputs and conditions are ordered deterministically so the reference set's
        /// version hash is stable across reloads.</summary>
        public static Contracts.SkillRecipe ToContract(EntitySkillRecipe entity)
        {
            return new Contracts.SkillRecipe
            {
                Id = entity.Id,
                ResultSkillId = entity.ResultSkillId,
                DesignerNotes = entity.DesignerNotes,
                RetiredAt = entity.RetiredAt,
                InputSkillIds = entity.Inputs
                    .Select(i => i.SkillId)
                    .OrderBy(id => id)
                    .ToList(),
                Conditions = entity.Conditions
                    .OrderBy(c => c.ProficiencyId)
                    .Select(c => new Contracts.SkillRecipeCondition
                    {
                        ProficiencyId = c.ProficiencyId,
                        MinLevel = c.MinLevel,
                    }).ToList(),
            };
        }

        /// <summary>Maps a read/authoring <see cref="Contracts.SkillRecipe"/> back to its entity graph (input
        /// skills and proficiency-level conditions) for the content seeder.</summary>
        public static EntitySkillRecipe ToEntity(Contracts.SkillRecipe contract)
        {
            return new EntitySkillRecipe
            {
                Id = contract.Id,
                ResultSkillId = contract.ResultSkillId,
                DesignerNotes = contract.DesignerNotes,
                RetiredAt = contract.RetiredAt,
                Inputs = contract.InputSkillIds
                    .Select(skillId => new EntitySkillRecipeInput
                    {
                        RecipeId = contract.Id,
                        SkillId = skillId,
                    }).ToList(),
                Conditions = contract.Conditions
                    .Select(condition => new EntitySkillRecipeCondition
                    {
                        RecipeId = contract.Id,
                        ProficiencyId = condition.ProficiencyId,
                        MinLevel = condition.MinLevel,
                    }).ToList(),
            };
        }

        /// <summary>Maps an entity <see cref="EntitySkillRecipe"/> (with its inputs and conditions loaded) to the
        /// lean domain <see cref="CoreSkillRecipe"/> the synthesis command validates against.</summary>
        public static CoreSkillRecipe ToCore(EntitySkillRecipe entity)
        {
            return new CoreSkillRecipe
            {
                Id = entity.Id,
                ResultSkillId = entity.ResultSkillId,
                IsRetired = entity.RetiredAt is not null,
                InputSkillIds = entity.Inputs
                    .Select(i => i.SkillId)
                    .OrderBy(id => id)
                    .ToList(),
                Conditions = entity.Conditions
                    .OrderBy(c => c.ProficiencyId)
                    .Select(c => new CoreRecipeCondition(c.ProficiencyId, c.MinLevel))
                    .ToList(),
            };
        }
    }
}
