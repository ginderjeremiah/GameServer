/* Player proficiency progress store — the player's per-proficiency level + XP
   (`GetPlayerProficiencies` socket command), shared across screens so the source is fetched once and
   stays consistent. The proficiency tree screen renders progress from it; the live player battler
   composes its per-level/milestone attribute bonuses (spike #982 area E) from these levels against the
   proficiency reference data, mirroring the backend battle snapshot. */

import { fetchSocketData, type IPlayerProficiency } from '$lib/api';
import { proficiencyModifiers } from '$lib/battle/proficiency-modifiers';
import type { AttributeModifier } from '$lib/battle/attribute-modifier';
import { staticData } from './static-data.svelte';

let proficiencies = $state<IPlayerProficiency[]>([]);
let loaded = $state(false);
let error = $state(false);
/** Coalesces concurrent callers (game boot + screen mount) onto one request. */
let inFlight: Promise<void> | undefined;

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

	/** Fetch the player's proficiency progress. Idempotent — only hits the network the first time
	 *  unless `force` re-fetches the latest values. */
	async load(force = false) {
		if (loaded && !force) {
			return;
		}
		if (!inFlight) {
			inFlight = fetchProficiencies().finally(() => {
				inFlight = undefined;
			});
		}
		await inFlight;
	},

	/** Reset to the unloaded state (e.g. on logout / session replacement). */
	reset() {
		proficiencies = [];
		loaded = false;
		error = false;
		inFlight = undefined;
	}
};
