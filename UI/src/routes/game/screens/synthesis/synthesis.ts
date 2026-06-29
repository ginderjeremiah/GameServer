/* Synthesis screen — the pure logic core.

   The delivered recipe reference data (`staticData.skillRecipes`), the skill catalogue
   (`staticData.skills`) and the player's owned skills + proficiency levels are composed into the
   per-recipe view-models the screen renders. This module is framework-free so the whole reveal/gating
   state machine is unit-testable without rendering; the reactive `SynthesisView` (synthesis-view.svelte.ts)
   only wires these functions to the live stores, owns selection, and runs the synthesize action.

   Discovery is the spike's **conservative hinted reveal** (spike #1125 decision 9): a recipe is fully
   shown — result, inputs, and any unmet proficiency condition rendered as the gate — once the player owns
   *all* its input skills; before that, a recipe the player owns *some* (but not all) inputs of is a vague
   hint (its result and missing inputs masked); a recipe the player owns *no* inputs of is hidden entirely.
   Availability is derived client-side here (there is no per-player synthesis table) from the owned skills,
   the proficiency levels, and the recipes — all already on the client. */

import type { IProficiency, ISkill, ISkillRecipe } from '$lib/api';
import { isRecipeCreatable } from '$lib/common';

/** A recipe's reveal/gating state. `hidden` recipes (no owned input) are dropped from the view-model
 *  entirely, so this enumerates only the rendered states:
 *  - `hinted` — owns some but not all inputs; result + missing inputs masked ("combines with something").
 *  - `gated` — owns every input but an unmet proficiency condition blocks synthesis.
 *  - `ready` — owns every input and every condition is met; synthesizable now.
 *  - `done` — the result skill is already owned (synthesis is one-time / idempotent). */
export type RecipeState = 'hinted' | 'gated' | 'ready' | 'done';

/** One input slot of a recipe. The skill is resolved only when the player owns it — an unowned input on
 *  a hinted recipe is masked to keep the pairing a secret (the conservative reveal). */
export interface RecipeInputView {
	skillId: number;
	owned: boolean;
	/** The resolved input skill, present only when owned. */
	skill?: ISkill;
}

/** A resolved proficiency condition on a recipe — the gate the player must master to synthesize it. */
export interface RecipeConditionView {
	proficiencyId: number;
	name: string;
	minLevel: number;
	currentLevel: number;
	met: boolean;
}

/** A recipe rendered on the screen: its reveal state, its (possibly masked) inputs, its resolved
 *  conditions, and — once revealed — the result skill. */
export interface RecipeView {
	id: number;
	state: RecipeState;
	/** Inputs in authored order; an unowned input is masked (`skill` absent) while the recipe is hinted. */
	inputs: RecipeInputView[];
	ownedInputCount: number;
	inputCount: number;
	/** Resolved proficiency conditions (empty when the recipe has none). Rendered as the gate when the
	 *  recipe is revealed; meaningless while hinted (the result is masked), so the screen hides them then. */
	conditions: RecipeConditionView[];
	/** The result skill — revealed only once every input is owned (`ready`/`gated`/`done`); `undefined`
	 *  while the recipe is a hint, so the result identity stays sealed until the player gathers the inputs. */
	result?: ISkill;
}

/** Rank for the default list ordering — actionable recipes first, finished ones last. */
const STATE_RANK: Record<RecipeState, number> = { ready: 0, gated: 1, hinted: 2, done: 3 };

/** Short display label per recipe state (the list tag + filter chips). */
export const RECIPE_STATE_LABEL: Record<RecipeState, string> = {
	ready: 'Ready',
	gated: 'Gated',
	hinted: 'Undiscovered',
	done: 'Synthesized'
};

/** The themeable accent CSS variable for a recipe state (single source for the screen's state colours,
 *  per the "colours by semantic intent" rule): ready → action, gated → mastery gold, hinted → muted,
 *  done → success. */
export const recipeStateAccent = (state: RecipeState): string =>
	({
		ready: 'var(--accent)',
		gated: 'var(--gold)',
		hinted: 'var(--text-muted)',
		done: 'var(--success)'
	})[state];

/**
 * Composes the recipe/skill/proficiency reference data and the player's owned skills + proficiency levels
 * into the per-recipe view-models the screen renders — the pure logic core.
 *
 * Retired recipes are excluded (a retired recipe "stops being offered"; an already-synthesized result
 * persists as a normal skill regardless — spike #1125 decision 12), as are recipes whose result skill is
 * missing from the catalogue. Hidden recipes (the player owns none of the inputs) are dropped, so the
 * result is exactly the **discovered** recipes, ordered by state then id.
 */
export function buildSynthesis(
	recipes: readonly ISkillRecipe[],
	skills: readonly ISkill[],
	proficiencies: readonly IProficiency[],
	ownedSkillIds: ReadonlySet<number>,
	proficiencyLevels: ReadonlyMap<number, number>
): RecipeView[] {
	const views: RecipeView[] = [];
	for (const recipe of recipes) {
		if (recipe.retiredAt) {
			continue;
		}
		// Reference catalogues are zero-based-id, resolved by array index (frontend.md → Reference Data).
		const result = skills[recipe.resultSkillId];
		if (!result) {
			continue;
		}

		const inputs: RecipeInputView[] = recipe.inputSkillIds.map((skillId) => {
			const owned = ownedSkillIds.has(skillId);
			return { skillId, owned, skill: owned ? skills[skillId] : undefined };
		});
		const ownedInputCount = inputs.filter((input) => input.owned).length;
		const inputCount = inputs.length;

		const synthesized = ownedSkillIds.has(recipe.resultSkillId);
		// A recipe with no owned inputs that the player hasn't somehow already synthesized stays hidden —
		// discovery is gathering ingredients.
		if (!synthesized && ownedInputCount === 0) {
			continue;
		}

		const conditions = recipe.conditions.map((condition): RecipeConditionView => {
			const currentLevel = proficiencyLevels.get(condition.proficiencyId) ?? 0;
			return {
				proficiencyId: condition.proficiencyId,
				name: proficiencies[condition.proficiencyId]?.name ?? '',
				minLevel: condition.minLevel,
				currentLevel,
				met: currentLevel >= condition.minLevel
			};
		});

		const revealed = synthesized || ownedInputCount === inputCount;
		let state: RecipeState;
		if (synthesized) {
			state = 'done';
		} else if (!revealed) {
			state = 'hinted';
		} else if (isRecipeCreatable(recipe, ownedSkillIds, proficiencyLevels)) {
			// `ready` is exactly "creatable now" — the shared predicate the new-recipe feedback uses, so the
			// screen's actionable state and the toast can never diverge. At this branch every input is owned
			// and the result is unowned, so the predicate reduces to "every condition met".
			state = 'ready';
		} else {
			state = 'gated';
		}

		views.push({
			id: recipe.id,
			state,
			inputs,
			ownedInputCount,
			inputCount,
			conditions,
			result: revealed ? result : undefined
		});
	}

	views.sort((a, b) => STATE_RANK[a.state] - STATE_RANK[b.state] || a.id - b.id);
	return views;
}

/** The recipe the screen opens to: the first actionable (lowest state-rank) recipe, i.e. the first row
 *  in the built list. `undefined` when nothing is discovered. */
export function representativeRecipe(recipes: readonly RecipeView[]): RecipeView | undefined {
	return recipes[0];
}
