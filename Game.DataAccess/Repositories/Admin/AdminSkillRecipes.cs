using Game.Abstractions.Contracts.Admin;
using Game.Abstractions.DataAccess.Admin;
using Game.Core;
using Game.DataAccess.Repositories.Caching;
using Contracts = Game.Abstractions.Contracts;
using Entities = Game.Infrastructure.Entities;

namespace Game.DataAccess.Repositories.Admin
{
    /// <summary>
    /// Content Authoring persistence for skill-synthesis recipes and their related collections (spike #1125).
    /// Reuses the cached entity lookups for existence/diff and builds fresh, navigation-free entities for every
    /// write. The identity save is retire-only (no hard delete); the relationship setters reconcile a full
    /// desired set. The recipe graph is kept acyclic both here (a clean failure before the write commits) and as
    /// a build-time cache invariant.
    /// </summary>
    internal class AdminSkillRecipes(
        ISkillRecipeEntityCache recipes,
        ISkillEntityCache skills,
        IProficiencyEntityCache proficiencies,
        IEntityStore entityStore) : IAdminSkillRecipes
    {
        private readonly ISkillRecipeEntityCache _recipes = recipes;
        private readonly ISkillEntityCache _skills = skills;
        private readonly IProficiencyEntityCache _proficiencies = proficiencies;
        private readonly IEntityStore _entityStore = entityStore;

        public AdminSaveResult SaveSkillRecipes(IReadOnlyList<Change<Contracts.SkillRecipe>> changes)
        {
            // Anti-tamper: a recipe's result is a permanent player grant, so the skill must exist, be live, and
            // declare itself Synthesis-acquirable (the flag is intent; this reference is reality). Rejected up
            // front before anything is staged.
            if (FindResultSkillViolation(changes) is { } rejection)
            {
                return rejection;
            }

            // Changing/adding a result skill can make the recipe graph cyclic (a skill its own ancestor). Build
            // the prospective graph — every live recipe's edges with this batch's result changes folded in — and
            // reject a cycle before anything commits. (A fresh Add has no inputs yet, so it adds no edge.)
            if (FindRecipeCycle(BuildProspectiveGraphForBatch(changes)) is { } cycleRejection)
            {
                return cycleRejection;
            }

            // One skill = one producing (non-retired) recipe (#1362): the synthesis graph keys revealed results
            // by skill id, so two live recipes producing the same skill would collapse to one node. Reject before
            // anything commits.
            if (FindDuplicateResultViolation(changes) is { } duplicateRejection)
            {
                return duplicateRejection;
            }

            return ChangeSetProcessor.Apply(changes,
                add: item => _entityStore.Insert(new Entities.SkillRecipe
                {
                    ResultSkillId = item.ResultSkillId,
                }),
                edit: item => _entityStore.Update(new Entities.SkillRecipe
                {
                    Id = item.Id,
                    ResultSkillId = item.ResultSkillId,
                    RetiredAt = item.RetiredAt,
                }),
                key: item => item.Id,
                resourceName: "skill recipe",
                editExists: item => _recipes.LookupSkillRecipe(item.Id) is not null);
        }

        public AdminSaveResult SetInputs(SetSkillRecipeInputsData data)
        {
            var recipe = _recipes.LookupSkillRecipe(data.Id);
            if (recipe is null)
            {
                return AdminSaveResult.NotFound("Skill recipe");
            }

            // A recipe combines at least one owned skill; an input-less recipe would synthesize from nothing.
            if (data.SkillIds.Count == 0)
            {
                return AdminSaveResult.Failure("A skill recipe must have at least one input skill.");
            }

            foreach (var skillId in data.SkillIds)
            {
                if (skillId == recipe.ResultSkillId)
                {
                    return AdminSaveResult.Failure("A recipe's inputs cannot include its own result skill.");
                }

                var skill = _skills.LookupSkill(skillId);
                if (skill is null)
                {
                    return AdminSaveResult.Failure($"Input skill {skillId} does not exist.");
                }

                if (skill.RetiredAt is not null)
                {
                    return AdminSaveResult.Failure($"Input skill '{skill.Name}' is retired and cannot be a recipe input.");
                }
            }

            // The inputs are this recipe's dependency edges; reject a set that would cycle the graph.
            if (FindRecipeCycle(BuildProspectiveGraphForInputs(recipe, data.SkillIds)) is { } cycleRejection)
            {
                return cycleRejection;
            }

            return ChildCollectionReconciler.Reconcile(
                existing: recipe.Inputs,
                desired: data.SkillIds,
                existingKey: i => i.SkillId,
                desiredKey: id => id,
                delete: i => _entityStore.Delete(new Entities.SkillRecipeInput
                {
                    RecipeId = recipe.Id,
                    SkillId = i.SkillId,
                }),
                insert: id => _entityStore.Insert(new Entities.SkillRecipeInput
                {
                    RecipeId = recipe.Id,
                    SkillId = id,
                }),
                resourceName: "skill recipe input");
        }

        public AdminSaveResult SetConditions(SetSkillRecipeConditionsData data)
        {
            var recipe = _recipes.LookupSkillRecipe(data.Id);
            if (recipe is null)
            {
                return AdminSaveResult.NotFound("Skill recipe");
            }

            foreach (var condition in data.Conditions)
            {
                var proficiency = _proficiencies.LookupProficiency(condition.ProficiencyId);
                if (proficiency is null)
                {
                    return AdminSaveResult.Failure($"Condition proficiency {condition.ProficiencyId} does not exist.");
                }

                // A gate is only meaningful at a level the proficiency can actually reach; level 0 is the
                // just-opened state, so a real gate starts at 1.
                if (condition.MinLevel < 1 || condition.MinLevel > proficiency.MaxLevel)
                {
                    return AdminSaveResult.Failure(
                        $"Condition min level {condition.MinLevel} is out of range (must be between 1 and the cap of {proficiency.MaxLevel}).");
                }
            }

            return ChildCollectionReconciler.Reconcile(
                existing: recipe.Conditions,
                desired: data.Conditions,
                existingKey: c => c.ProficiencyId,
                desiredKey: c => c.ProficiencyId,
                delete: c => _entityStore.Delete(new Entities.SkillRecipeCondition
                {
                    RecipeId = recipe.Id,
                    ProficiencyId = c.ProficiencyId,
                }),
                insert: c => _entityStore.Insert(ToConditionEntity(recipe.Id, c)),
                resourceName: "skill recipe condition",
                update: c => _entityStore.Update(ToConditionEntity(recipe.Id, c)));
        }

        private static Entities.SkillRecipeCondition ToConditionEntity(int recipeId, Contracts.SkillRecipeCondition condition)
        {
            return new Entities.SkillRecipeCondition
            {
                RecipeId = recipeId,
                ProficiencyId = condition.ProficiencyId,
                MinLevel = condition.MinLevel,
            };
        }

        /// <summary>Returns a rejection if any added/edited recipe's result skill does not exist, is retired, or
        /// is not Synthesis-flagged, else null.</summary>
        private AdminSaveResult? FindResultSkillViolation(IReadOnlyList<Change<Contracts.SkillRecipe>> changes)
        {
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Delete)
                {
                    continue;
                }

                var skill = _skills.LookupSkill(change.Item.ResultSkillId);
                if (skill is null)
                {
                    return AdminSaveResult.Failure($"Result skill {change.Item.ResultSkillId} does not exist.");
                }

                if (skill.RetiredAt is not null)
                {
                    return AdminSaveResult.Failure($"Result skill '{skill.Name}' is retired and cannot be a recipe result.");
                }

                if (!((ESkillAcquisition)skill.Acquisition).HasFlag(ESkillAcquisition.Synthesis))
                {
                    return AdminSaveResult.Failure(
                        $"Skill '{skill.Name}' is not flagged as Synthesis-acquirable and cannot be a recipe result.");
                }
            }

            return null;
        }

        /// <summary>Returns a rejection if the batch would leave two non-retired recipes producing the same
        /// result skill (one skill = one producing recipe, #1362), else null. Builds the prospective live-result
        /// map — live recipes with each Edit re-pointing/retiring its recipe and each Add contributing a new
        /// producer — then flags a result claimed more than once.</summary>
        private AdminSaveResult? FindDuplicateResultViolation(IReadOnlyList<Change<Contracts.SkillRecipe>> changes)
        {
            var resultByRecipe = _recipes.AllSkillRecipeEntities()
                .Where(r => r.RetiredAt is null)
                .ToDictionary(r => r.Id, r => r.ResultSkillId);

            // An Add's real id is store-generated and unknown here, so a descending sentinel keeps each new
            // producer distinct from every real recipe id and from the other Adds in the batch.
            var addKey = 0;
            foreach (var change in changes)
            {
                if (change.ChangeType == EChangeType.Add)
                {
                    resultByRecipe[--addKey] = change.Item.ResultSkillId;
                }
                else if (change.ChangeType == EChangeType.Edit)
                {
                    if (change.Item.RetiredAt is not null)
                    {
                        resultByRecipe.Remove(change.Item.Id);
                    }
                    else if (_recipes.LookupSkillRecipe(change.Item.Id) is not null)
                    {
                        // Re-points a live recipe's result, or brings an un-retired one back into circulation.
                        resultByRecipe[change.Item.Id] = change.Item.ResultSkillId;
                    }
                }
            }

            var duplicate = resultByRecipe.Values.GroupBy(id => id).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is null)
            {
                return null;
            }

            var skill = _skills.LookupSkill(duplicate.Key);
            return AdminSaveResult.Failure(
                $"Skill '{skill?.Name ?? duplicate.Key.ToString()}' would be produced by more than one active recipe; each skill may have only one producing recipe.");
        }

        /// <summary>The live recipe graph as (result, inputs) pairs keyed by recipe id — the base every
        /// prospective-graph builder folds a proposed change into. Retired recipes are inert, so excluded.</summary>
        private Dictionary<int, (int ResultSkillId, IReadOnlyList<int> InputSkillIds)> LiveRecipeEdges()
        {
            return _recipes.AllSkillRecipeEntities()
                .Where(r => r.RetiredAt is null)
                .ToDictionary(
                    r => r.Id,
                    r => (r.ResultSkillId, (IReadOnlyList<int>)r.Inputs.Select(i => i.SkillId).ToList()));
        }

        /// <summary>The prospective edges after applying a <c>SetInputs</c> on <paramref name="recipe"/>: the
        /// live edges with this recipe's inputs replaced (it is omitted while retired, since it is then inert).</summary>
        private IEnumerable<(int ResultSkillId, IReadOnlyList<int> InputSkillIds)> BuildProspectiveGraphForInputs(
            Entities.SkillRecipe recipe, IReadOnlyList<int> desiredInputs)
        {
            var edges = LiveRecipeEdges();
            if (recipe.RetiredAt is null)
            {
                edges[recipe.Id] = (recipe.ResultSkillId, desiredInputs);
            }

            return edges.Values;
        }

        /// <summary>The prospective edges after applying an identity batch: each Edit replaces its recipe's
        /// result (keeping its inputs) or removes it when retired; each Add contributes no edge (no inputs yet).</summary>
        private IEnumerable<(int ResultSkillId, IReadOnlyList<int> InputSkillIds)> BuildProspectiveGraphForBatch(
            IReadOnlyList<Change<Contracts.SkillRecipe>> changes)
        {
            var edges = LiveRecipeEdges();
            foreach (var change in changes)
            {
                if (change.ChangeType != EChangeType.Edit)
                {
                    continue;
                }

                if (change.Item.RetiredAt is not null)
                {
                    edges.Remove(change.Item.Id);
                }
                else if (edges.TryGetValue(change.Item.Id, out var existing))
                {
                    edges[change.Item.Id] = (change.Item.ResultSkillId, existing.InputSkillIds);
                }
                else if (_recipes.LookupSkillRecipe(change.Item.Id) is { } entity)
                {
                    // Un-retiring a recipe brings it back into the graph with its stored inputs.
                    edges[change.Item.Id] = (change.Item.ResultSkillId, entity.Inputs.Select(i => i.SkillId).ToList());
                }
            }

            return edges.Values;
        }

        private static AdminSaveResult? FindRecipeCycle(IEnumerable<(int ResultSkillId, IReadOnlyList<int> InputSkillIds)> prospectiveEdges)
        {
            var graph = SkillRecipeGraph.BuildDependencyGraph(prospectiveEdges);
            if (SkillRecipeGraph.TryFindCycle(graph, out var cycle))
            {
                return AdminSaveResult.Failure(
                    $"This change would create a cycle in the recipe graph (a skill cannot be synthesized from itself): {string.Join(" -> ", cycle)}.");
            }

            return null;
        }
    }
}
