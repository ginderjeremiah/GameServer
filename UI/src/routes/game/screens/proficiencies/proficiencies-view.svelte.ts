/* Proficiencies screen — "The Lexicon": the reactive view-model.

   Wires the pure logic core (proficiencies-lexicon.ts) to the live stores and owns the rail/spine
   selection. Loading and error are set by the screen after it (re-)fetches the player's progress
   (mirroring the Statistics screen); the derived `paths` recompute as the reference data and progress
   arrive. The pure derivation lives in the framework-free module so it stays unit-testable without
   rendering. */

import { applies, primaryDamageType } from '$lib/battle/damage-types';
import { inventoryManager, playerManager } from '$lib/engine';
import { playerProficiencies, staticData } from '$stores';
import { buildLexicon, representativeTier, type TierView } from './proficiencies-lexicon';

export class ProficienciesView {
	/** True until the screen's first progress fetch settles. */
	loading = $state(true);
	/** Set when the progress fetch fails (distinct from a genuinely empty lexicon). */
	error = $state(false);
	/** The rail's selected path; null falls back to the first discovered path. */
	selectedPathId = $state<number | null>(null);
	/** The spine's selected tier; null falls back to the selected path's representative tier. */
	selectedTierId = $state<number | null>(null);

	/** The discovered paths, each an ordered spine — recomputed as progress / reference data change. The
	 *  firing skills (which decide the `training` state) are the union the battler actually fires: the
	 *  selected loadout plus the innate item-granted skills (see `battle-engine` → `player.reset`). */
	readonly paths = $derived.by(() => {
		// The activity keys the player's firing skills (selected loadout + innate item grants) cover: a path
		// shows as 'training' when a firing skill's damage type routes to its key (#1318). Damage-type keys
		// share numeric values with the activity keys, so they are compared directly against path.activityKey.
		const firingActivityKeys = [...playerManager.selectedSkills, ...inventoryManager.grantedSkillIds].flatMap((id) => {
			const skill = staticData.skills?.[id];
			// The skill's primary (highest-weight) portion type routes the training-state highlight; a
			// multi-typed skill's secondary portions stay a #1385+ concern.
			return skill ? applies(primaryDamageType(skill.damagePortions)) : [];
		});
		return buildLexicon(
			staticData.proficiencies ?? [],
			staticData.paths ?? [],
			playerProficiencies.all,
			firingActivityKeys
		);
	});

	/** True when the player has not discovered any path yet (the new-player empty state). */
	readonly isEmpty = $derived(this.paths.length === 0);

	/** The selected path, defaulting to the first discovered one. */
	readonly selectedPath = $derived(this.paths.find((p) => p.id === this.selectedPathId) ?? this.paths[0]);

	/** The selected tier within the selected path, defaulting to that path's representative tier. */
	readonly selectedTier = $derived.by((): TierView | undefined => {
		const path = this.selectedPath;
		if (!path) {
			return undefined;
		}
		return path.tiers.find((t) => t.id === this.selectedTierId) ?? representativeTier(path);
	});

	/** Select a path from the rail, resetting the spine selection to that path's representative tier. */
	selectPath(id: number): void {
		this.selectedPathId = id;
		this.selectedTierId = null;
	}

	/** Select a tier from the spine, keeping its owning path selected. */
	selectTier(id: number): void {
		const owner = this.paths.find((p) => p.tiers.some((t) => t.id === id));
		if (owner) {
			this.selectedPathId = owner.id;
		}
		this.selectedTierId = id;
	}
}
