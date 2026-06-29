/* Player-facing feedback for skill synthesis (spike #1125 area F). Mirrors proficiency-feedback /
   challenge-unlocks: the pure "what can the player synthesize right now" derivation and the announcement
   phrasing live here so the toast path and the Synthesis screen share one definition of "creatable" and
   never drift apart. Synthesizing itself is player-driven and deliberately un-toasted (spike #1125 area F);
   this is only for a recipe that *becomes* creatable from background progress — a milestone-granted input
   skill completing a recipe's inputs, or a proficiency level-up crossing a recipe's condition gate. */

import type { ISkillRecipe } from '$lib/api';

/**
 * Whether a recipe can be synthesized right now: its result is not already owned (synthesis is one-time /
 * idempotent), every input skill is owned, and every proficiency condition is met (a missing proficiency
 * counts as level 0). This is the single definition of "creatable", shared by the Synthesis screen's
 * `ready` state and the new-recipe-available feedback so the two never diverge.
 */
export function isRecipeCreatable(
	recipe: ISkillRecipe,
	ownedSkillIds: ReadonlySet<number>,
	proficiencyLevels: ReadonlyMap<number, number>
): boolean {
	if (ownedSkillIds.has(recipe.resultSkillId)) {
		return false;
	}
	if (!recipe.inputSkillIds.every((skillId) => ownedSkillIds.has(skillId))) {
		return false;
	}
	return recipe.conditions.every(
		(condition) => (proficiencyLevels.get(condition.proficiencyId) ?? 0) >= condition.minLevel
	);
}

/**
 * The ids of every currently-creatable recipe (see {@link isRecipeCreatable}). Retired recipes are excluded
 * — a retired recipe stops being offered, so it can never "become available" (spike #1125 decision 12).
 * Compared before/after a progress push to surface the recipes that newly became creatable.
 */
export function creatableRecipeIds(
	recipes: readonly ISkillRecipe[],
	ownedSkillIds: ReadonlySet<number>,
	proficiencyLevels: ReadonlyMap<number, number>
): Set<number> {
	const ids = new Set<number>();
	for (const recipe of recipes) {
		if (!recipe.retiredAt && isRecipeCreatable(recipe, ownedSkillIds, proficiencyLevels)) {
			ids.add(recipe.id);
		}
	}
	return ids;
}

/** The "new recipe available" toast message, naming the result skill the player can now synthesize. */
export function recipeAvailableMessage(resultSkillName: string): string {
	return `New recipe available: ${resultSkillName}`;
}
