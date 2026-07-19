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
import { layoutSynthesisGraph } from './synthesis-graph';

/** A recipe-list filter — `all` plus one per rendered {@link RecipeState}. */
export type RecipeFilter = 'all' | RecipeState;

/** The screen's two views: the `bench` recipe list → bench → dossier, and the `web` recipe-graph canvas. */
export type SynthesisMode = 'bench' | 'web';

export class SynthesisView {
	/** True until the screen's first proficiency-progress fetch settles. */
	loading = $state(true);
	/** Set when the progress fetch fails (distinct from a genuinely empty recipe list). */
	error = $state(false);
	/** The selected recipe; null falls back to the representative (first actionable) recipe. */
	selectedRecipeId = $state<number | null>(null);
	/** The active list filter. */
	filter = $state<RecipeFilter>('all');
	/** The active view — the bench list or the recipe-graph web. */
	mode = $state<SynthesisMode>('bench');
	/** True while a `SynthesizeSkill` command is in flight, so the CTA can't be double-submitted. */
	synthesizing = $state(false);

	/** The player's owned (unlocked) skill ids — the inputs synthesis matches against. Innate item-granted
	 *  skills are deliberately excluded (they are not in `unlockedSkills`), mirroring the backend rule. */
	// eslint-disable-next-line svelte/prefer-svelte-reactivity
	readonly ownedSkillIds = $derived(new Set(playerManager.unlockedSkills.map((s) => s.skillId)));

	/** Previous {@link proficiencyLevels} value, for the reference-stability check below. Plain (not
	 *  `$state`) so writing it mid-derivation isn't a reactive mutation. */
	#previousLevels: Map<number, number> | undefined;

	/** The player's current level per proficiency (a missing proficiency counts as level 0 in gating).
	 *  Keeps the previous Map's identity when every level is unchanged: `playerProficiencies.all`
	 *  reassigns its array wholesale on every `ProficiencyXpGained` push (deliberately, for by-reference
	 *  change detection elsewhere) even when xp moved but no level did, and this is the value {@link
	 *  recipes} and {@link graph} key their own recomputation on — without this, the whole recipe list
	 *  and graph layout would re-derive on every battle victory for no visible change. */
	readonly proficiencyLevels = $derived.by(() => {
		const all = playerProficiencies.all;
		const previous = this.#previousLevels;
		if (previous && previous.size === all.length && all.every((p) => previous.get(p.proficiencyId) === p.level)) {
			return previous;
		}
		// eslint-disable-next-line svelte/prefer-svelte-reactivity
		const next = new Map(all.map((p) => [p.proficiencyId, p.level]));
		this.#previousLevels = next;
		return next;
	});

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

	/** The discovered recipes laid out as the recipe-graph DAG — input → fusion → result, with chaining.
	 *  Recomputed from the same `recipes` view-models, so the graph and the list never diverge. */
	readonly graph = $derived(layoutSynthesisGraph(this.recipes));

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

	/** Switch between the bench and web views. */
	setMode(mode: SynthesisMode): void {
		this.mode = mode;
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
