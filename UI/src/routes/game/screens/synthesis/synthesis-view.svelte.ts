/* Synthesis screen — the reactive view-model.

   Wires the pure derivation (synthesis.ts) to the live stores, owns the recipe selection and the filter,
   and runs the synthesize action. Loading/error are set by the screen after it (re-)fetches the player's
   proficiency progress (mirroring the Proficiencies screen) so gating reflects play since the store was
   last loaded at boot; the recipes/skills/proficiencies reference data is already in `staticData`. The
   pure derivation lives in the framework-free module so it stays unit-testable without rendering. */

import { apiSocket } from '$lib/api';
import { playerManager } from '$lib/engine';
import { playerProficiencies, staticData, toastError } from '$stores';
import { buildSynthesis, representativeRecipe, type RecipeState, type RecipeView } from './synthesis';

/** A recipe-list filter — `all` plus one per rendered {@link RecipeState}. */
export type RecipeFilter = 'all' | RecipeState;

export class SynthesisView {
	/** True until the screen's first proficiency-progress fetch settles. */
	loading = $state(true);
	/** Set when the progress fetch fails (distinct from a genuinely empty recipe list). */
	error = $state(false);
	/** The selected recipe; null falls back to the representative (first actionable) recipe. */
	selectedRecipeId = $state<number | null>(null);
	/** The active list filter. */
	filter = $state<RecipeFilter>('all');
	/** True while a `SynthesizeSkill` command is in flight, so the CTA can't be double-submitted. */
	synthesizing = $state(false);

	/** The player's owned (unlocked) skill ids — the inputs synthesis matches against. Innate item-granted
	 *  skills are deliberately excluded (they are not in `unlockedSkills`), mirroring the backend rule. */
	// eslint-disable-next-line svelte/prefer-svelte-reactivity
	readonly ownedSkillIds = $derived(new Set(playerManager.unlockedSkills.map((s) => s.skillId)));

	/** The player's current level per proficiency (a missing proficiency counts as level 0 in gating). */
	// eslint-disable-next-line svelte/prefer-svelte-reactivity
	readonly proficiencyLevels = $derived(new Map(playerProficiencies.all.map((p) => [p.proficiencyId, p.level])));

	/** The discovered recipes with their reveal/gating state — recomputed as owned skills, proficiency
	 *  levels, and reference data change. */
	readonly recipes = $derived(
		buildSynthesis(
			staticData.skillRecipes ?? [],
			staticData.skills ?? [],
			staticData.proficiencies ?? [],
			this.ownedSkillIds,
			this.proficiencyLevels
		)
	);

	/** Per-state counts across all discovered recipes (drives the filter chips + header). */
	readonly counts = $derived.by(() => {
		const counts: Record<RecipeState, number> = { ready: 0, gated: 0, hinted: 0, done: 0 };
		for (const recipe of this.recipes) {
			counts[recipe.state]++;
		}
		return counts;
	});

	/** The recipes shown after applying the active filter. */
	readonly visibleRecipes = $derived(
		this.filter === 'all' ? this.recipes : this.recipes.filter((r) => r.state === this.filter)
	);

	/** The number of discovered (non-hidden) recipes — the header/“all” count. */
	readonly discoveredCount = $derived(this.recipes.length);

	/** True when the player has discovered no recipe yet (the new-player empty state). */
	readonly isEmpty = $derived(this.recipes.length === 0);

	/** The selected recipe, defaulting to the representative (first actionable) one — resolved against the
	 *  full list so a selection survives a filter change that would hide it. */
	readonly selected = $derived(
		this.recipes.find((r) => r.id === this.selectedRecipeId) ?? representativeRecipe(this.recipes)
	);

	/** Select a recipe from the list. */
	select(id: number): void {
		this.selectedRecipeId = id;
	}

	/** Set the active list filter. */
	setFilter(filter: RecipeFilter): void {
		this.filter = filter;
	}

	/**
	 * Forge a recipe: send the authoritative `SynthesizeSkill` command and, on success, unlock the result
	 * onto the player so the screen (and the rest of the app) reflects the new skill immediately — the
	 * recipe flips to `done` reactively. The command is the anti-cheat boundary; the client only supplies
	 * the recipe id. A no-op unless the recipe is `ready` (every input owned + every condition met) and no
	 * synthesize is already in flight. Returns true when the result was unlocked.
	 */
	async synthesize(recipe: RecipeView): Promise<boolean> {
		if (recipe.state !== 'ready' || this.synthesizing) {
			return false;
		}
		this.synthesizing = true;
		try {
			const response = await apiSocket.sendSocketCommand('SynthesizeSkill', recipe.id);
			if (response.error || response.data?.resultSkillId == null) {
				toastError('The skill could not be synthesized. Please try again.');
				return false;
			}
			// Idempotent on the manager too — re-synthesis is a no-op (mirrors the backend `UnlockSkill`).
			playerManager.addUnlockedSkill(response.data.resultSkillId);
			return true;
		} finally {
			this.synthesizing = false;
		}
	}
}
