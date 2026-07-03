/* Player proficiency progress store — the player's per-proficiency level + XP
   (`GetPlayerProficiencies` socket command), shared across screens so the source is fetched once and
   stays consistent. The proficiency tree screen renders progress from it; the live player battler
   composes its per-level/milestone attribute bonuses (spike #982 area E) from these levels against the
   proficiency reference data, mirroring the backend battle snapshot. */

import { fetchSocketData, type IPlayerProficiency, type IProficiencyXpGainedModel } from '$lib/api';
import { proficiencyModifiers } from '$lib/battle/proficiency-modifiers';
import type { AttributeModifier } from '$lib/battle/attribute-modifier';
import { CoalescedLoader } from '$lib/common/coalesced-loader';
import { staticData } from './static-data.svelte';

let proficiencies = $state<IPlayerProficiency[]>([]);
let loaded = $state(false);
let error = $state(false);

const fetchProficiencies = async () => {
	try {
		// fetchSocketData throws on a socket error, preserving this try/catch contract.
		proficiencies = (await fetchSocketData('GetPlayerProficiencies')) ?? [];
		error = false;
		loaded = true;
	} catch {
		// A failed load is not a genuine empty result; leave the live battler without proficiency
		// bonuses and let the next load retry. The proficiency screen surfaces the error to the user.
		error = true;
	}
};

/** Coalesces concurrent callers (game boot + screen mount) onto one request; a forced load
 *  issued mid-flight chains a fresh fetch so it never resolves with stale data. */
const loader = new CoalescedLoader(fetchProficiencies, () => loaded);

/**
 * The proficiency attribute bonuses for the player's current levels, composed against the proficiency
 * reference data — the modifiers fed into the live player battler at assembly so its attributes match the
 * backend battle snapshot (the one frontend↔backend parity surface proficiency adds, spike #982 area E).
 *
 * A `$derived`, so it recomputes only when the player's levels or the proficiency reference data change and
 * otherwise returns a stable reference: the battle engine's per-spawn change-detection (#811) compares it
 * by reference, so an idle farm with unchanged proficiencies skips the full battler re-derive. Proficiency
 * ids are the zero-based-id reference catalogue, resolved by array index (frontend.md → Reference Data).
 */
const battleModifiers = $derived.by((): AttributeModifier[] => {
	const reference = staticData.proficiencies;
	if (!reference) {
		return [];
	}
	return proficiencies.flatMap((player) => {
		const definition = reference[player.proficiencyId];
		return definition ? proficiencyModifiers(definition.levelModifiers, player.level) : [];
	});
});

export const playerProficiencies = {
	get all() {
		return proficiencies;
	},
	get loaded() {
		return loaded;
	},
	get error() {
		return error;
	},
	/** The player's proficiency attribute bonuses for the live battler (see {@link battleModifiers}). */
	get battleModifiers() {
		return battleModifiers;
	},

	/** The player's current level in a proficiency, or 0 if they have not opened it yet. Read before
	 *  {@link applyXpGained} to detect a level-up (the push carries the new level, not the prior one). */
	levelOf(proficiencyId: number) {
		return proficiencies.find((p) => p.proficiencyId === proficiencyId)?.level ?? 0;
	},

	/** Apply a `ProficiencyXpGained` push so progress-derived UI — the tree and the live battler's
	 *  attribute bonuses — reflects it immediately without a refetch. Each result's new level/xp is set
	 *  (inserting the proficiency if it was previously unopened), and any newly-opened proficiency the
	 *  battle did not also train is added at level 0 so it appears straight away. A later `load(force)`
	 *  reconciles against the authoritative server progress. The array is reassigned (not mutated) so the
	 *  `battleModifiers` derived recomputes and the battle engine's by-reference change detection fires. */
	applyXpGained(model: IProficiencyXpGainedModel) {
		// Update each already-tracked proficiency in place to its new level/xp.
		let next = proficiencies.map((existing): IPlayerProficiency => {
			const result = model.proficiencies.find((r) => r.proficiencyId === existing.proficiencyId);
			return result ? { proficiencyId: result.proficiencyId, level: result.newLevel, xp: result.newXp } : existing;
		});
		// Append any result for a proficiency the player had not yet opened.
		for (const result of model.proficiencies) {
			if (!next.some((p) => p.proficiencyId === result.proficiencyId)) {
				next = [...next, { proficiencyId: result.proficiencyId, level: result.newLevel, xp: result.newXp }];
			}
		}
		// Append any newly-opened proficiency the battle did not also train, at level 0.
		for (const opened of model.opened) {
			if (!next.some((p) => p.proficiencyId === opened.proficiencyId)) {
				next = [...next, { proficiencyId: opened.proficiencyId, level: 0, xp: 0 }];
			}
		}
		proficiencies = next;
	},

	/** Fetch the player's proficiency progress. Idempotent — only hits the network the first time
	 *  unless `force` re-fetches the latest values. */
	async load(force = false) {
		await loader.load(force);
	},

	/** Reset to the unloaded state (e.g. on logout / session replacement). */
	reset() {
		proficiencies = [];
		loaded = false;
		error = false;
		loader.reset();
	}
};
