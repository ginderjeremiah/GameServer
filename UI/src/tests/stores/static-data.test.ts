import { describe, it, expect, beforeEach } from 'vitest';
import { staticData } from '$stores/static-data.svelte';

// The reference-data slots the store exposes. Keeping the list here lets each test
// reset every slot and populate the full set without repeating the names inline.
const SLOTS = [
	'zones',
	'enemies',
	'items',
	'skills',
	'itemMods',
	'attributes',
	'challenges',
	'challengeTypes',
	'statisticTypes',
	'proficiencies',
	'paths',
	'classes',
	'skillRecipes',
	'lessons'
] as const;

const clearAll = () => {
	for (const slot of SLOTS) {
		staticData[slot] = undefined;
	}
};

const populateAll = () => {
	for (const slot of SLOTS) {
		staticData[slot] = [];
	}
};

beforeEach(clearAll);

describe('staticData store', () => {
	it('exposes each slot as undefined before it is loaded', () => {
		for (const slot of SLOTS) {
			expect(staticData[slot]).toBeUndefined();
		}
	});

	it('returns the value a slot was set to (including an empty array, distinct from undefined)', () => {
		const zones = [{ id: 1 }] as unknown as NonNullable<typeof staticData.zones>;
		staticData.zones = zones;
		expect(staticData.zones).toEqual(zones);

		staticData.enemies = [];
		expect(staticData.enemies).toEqual([]);
		expect(staticData.enemies).not.toBeUndefined();
	});

	it('reports loaded=false until every slot is populated', () => {
		expect(staticData.loaded).toBe(false);

		// Populate all but the last slot — still not loaded.
		for (const slot of SLOTS.slice(0, -1)) {
			staticData[slot] = [];
		}
		expect(staticData.loaded).toBe(false);

		// Populating the final slot flips it to loaded.
		staticData[SLOTS[SLOTS.length - 1]] = [];
		expect(staticData.loaded).toBe(true);
	});

	it('treats an empty array as loaded (not-loaded is detected by null, not emptiness)', () => {
		populateAll();
		expect(staticData.loaded).toBe(true);
	});

	it('reports loaded=false again if any slot is cleared after a full load', () => {
		populateAll();
		expect(staticData.loaded).toBe(true);

		staticData.skills = undefined;
		expect(staticData.loaded).toBe(false);
	});

	it('reset() clears every slot back to unloaded', () => {
		populateAll();
		expect(staticData.loaded).toBe(true);

		staticData.reset();

		for (const slot of SLOTS) {
			expect(staticData[slot]).toBeUndefined();
		}
		expect(staticData.loaded).toBe(false);
	});
});
