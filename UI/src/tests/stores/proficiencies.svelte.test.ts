import { describe, it, expect, beforeEach, vi } from 'vitest';

// The store reads over the socket via fetchSocketData; stub just that export while keeping the real
// IPlayerProficiency / IProficiency types (and the rest of the barrel) intact.
const { mockFetchSocket } = vi.hoisted(() => ({ mockFetchSocket: vi.fn() }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, fetchSocketData: mockFetchSocket };
});

import {
	EAttribute,
	EModifierType,
	type IPlayerProficiency,
	type IProficiency,
	type IProficiencyXpResultModel
} from '$lib/api';
import { EAttributeModifierSource } from '$lib/battle/attribute-modifier';
import { playerProficiencies } from '$stores/proficiencies.svelte';
import { staticData } from '$stores/static-data.svelte';

const playerProficiency = (proficiencyId: number, level: number, xp = 0): IPlayerProficiency => ({
	proficiencyId,
	level,
	xp
});

// A minimal proficiency reference record whose only meaningful payload here is its per-level modifiers.
const proficiency = (id: number, levelModifiers: IProficiency['levelModifiers']): IProficiency => ({
	id,
	name: `Proficiency ${id}`,
	description: '',
	designerNotes: '',
	iconPath: '',
	word: '',
	pronunciation: '',
	translation: '',
	pathId: 0,
	pathOrdinal: id,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1,
	levelModifiers,
	levelRewards: [],
	prerequisiteIds: []
});

const additive = (level: number, attributeId: EAttribute, amount: number) => ({
	level,
	attributeId,
	modifierTypeId: EModifierType.Additive,
	amount
});

describe('proficiencies store', () => {
	beforeEach(() => {
		playerProficiencies.reset();
		mockFetchSocket.mockReset();
		staticData.proficiencies = undefined;
	});

	it('loads the player proficiencies once and exposes them', async () => {
		mockFetchSocket.mockResolvedValue([playerProficiency(0, 3)]);

		await playerProficiencies.load();

		expect(playerProficiencies.loaded).toBe(true);
		expect(playerProficiencies.all).toEqual([playerProficiency(0, 3)]);
		expect(mockFetchSocket).toHaveBeenCalledWith('GetPlayerProficiencies');

		// Idempotent: a second (non-forced) load does not re-fetch.
		await playerProficiencies.load();
		expect(mockFetchSocket).toHaveBeenCalledTimes(1);
	});

	it('re-fetches when forced', async () => {
		mockFetchSocket.mockResolvedValue([]);
		await playerProficiencies.load();

		mockFetchSocket.mockResolvedValue([playerProficiency(1, 5)]);
		await playerProficiencies.load(true);

		expect(mockFetchSocket).toHaveBeenCalledTimes(2);
		expect(playerProficiencies.all).toEqual([playerProficiency(1, 5)]);
	});

	it('coalesces concurrent loads onto a single request', async () => {
		mockFetchSocket.mockResolvedValue([]);

		await Promise.all([playerProficiencies.load(), playerProficiencies.load()]);

		expect(mockFetchSocket).toHaveBeenCalledTimes(1);
	});

	it('a forced load issued mid-flight fetches fresh data instead of settling for the stale response', async () => {
		const stale = Promise.withResolvers<IPlayerProficiency[]>();
		mockFetchSocket.mockReturnValueOnce(stale.promise);

		const initial = playerProficiencies.load();
		const forced = playerProficiencies.load(true);
		expect(mockFetchSocket).toHaveBeenCalledTimes(1);

		// The in-flight response predates the force (e.g. divergence recovery after a failed push),
		// so the forced caller must get a second fetch — issued after the stale one settles — and its data.
		mockFetchSocket.mockResolvedValueOnce([playerProficiency(1, 5)]);
		stale.resolve([]);
		await forced;

		expect(mockFetchSocket).toHaveBeenCalledTimes(2);
		expect(playerProficiencies.all).toEqual([playerProficiency(1, 5)]);
		await initial;
	});

	it('flags an error and leaves proficiencies empty when the fetch fails', async () => {
		mockFetchSocket.mockRejectedValue(new Error('boom'));

		await playerProficiencies.load();

		expect(playerProficiencies.error).toBe(true);
		expect(playerProficiencies.all).toEqual([]);
		expect(playerProficiencies.loaded).toBe(false);
	});

	it('reset() clears state and allows a fresh load', async () => {
		mockFetchSocket.mockResolvedValue([playerProficiency(0, 3)]);
		await playerProficiencies.load();

		playerProficiencies.reset();
		expect(playerProficiencies.loaded).toBe(false);
		expect(playerProficiencies.all).toEqual([]);

		mockFetchSocket.mockResolvedValue([playerProficiency(2, 1)]);
		await playerProficiencies.load();
		expect(playerProficiencies.all).toEqual([playerProficiency(2, 1)]);
	});

	describe('levelOf', () => {
		it('returns the stored level for a known proficiency and 0 for an unopened one', async () => {
			mockFetchSocket.mockResolvedValue([playerProficiency(0, 3)]);
			await playerProficiencies.load();

			expect(playerProficiencies.levelOf(0)).toBe(3);
			expect(playerProficiencies.levelOf(7)).toBe(0);
		});
	});

	describe('applyXpGained', () => {
		const xpResult = (proficiencyId: number, newLevel: number, newXp: number): IProficiencyXpResultModel => ({
			proficiencyId,
			xpGained: 10,
			newLevel,
			newXp,
			milestonesCrossed: [],
			grantedSkillIds: []
		});

		it('updates an existing proficiency in place to its new level and xp', async () => {
			mockFetchSocket.mockResolvedValue([playerProficiency(0, 3, 20)]);
			await playerProficiencies.load();

			playerProficiencies.applyXpGained({ proficiencies: [xpResult(0, 4, 5)], opened: [] });

			expect(playerProficiencies.all).toEqual([playerProficiency(0, 4, 5)]);
		});

		it('inserts a proficiency that was not yet in the store', async () => {
			mockFetchSocket.mockResolvedValue([]);
			await playerProficiencies.load();

			playerProficiencies.applyXpGained({ proficiencies: [xpResult(2, 1, 8)], opened: [] });

			expect(playerProficiencies.all).toEqual([playerProficiency(2, 1, 8)]);
		});

		it('adds a newly-opened proficiency at level 0 when the battle did not also train it', async () => {
			mockFetchSocket.mockResolvedValue([]);
			await playerProficiencies.load();

			playerProficiencies.applyXpGained({
				proficiencies: [],
				opened: [{ proficiencyId: 5 }]
			});

			expect(playerProficiencies.all).toEqual([playerProficiency(5, 0, 0)]);
		});

		it('does not duplicate an opened proficiency the same push already trained', async () => {
			mockFetchSocket.mockResolvedValue([]);
			await playerProficiencies.load();

			playerProficiencies.applyXpGained({
				proficiencies: [xpResult(5, 1, 3)],
				opened: [{ proficiencyId: 5 }]
			});

			expect(playerProficiencies.all).toEqual([playerProficiency(5, 1, 3)]);
		});

		it('reassigns the array so battleModifiers re-derives after a level change', async () => {
			staticData.proficiencies = [proficiency(0, [additive(1, EAttribute.Strength, 4)])];
			mockFetchSocket.mockResolvedValue([playerProficiency(0, 1)]);
			await playerProficiencies.load();

			const before = playerProficiencies.battleModifiers;
			playerProficiencies.applyXpGained({ proficiencies: [xpResult(0, 2, 0)], opened: [] });

			expect(playerProficiencies.battleModifiers).not.toBe(before);
			expect(playerProficiencies.levelOf(0)).toBe(2);
		});
	});

	describe('battleModifiers', () => {
		it('is empty before the proficiency reference data is loaded', async () => {
			mockFetchSocket.mockResolvedValue([playerProficiency(0, 2)]);
			await playerProficiencies.load();

			expect(playerProficiencies.battleModifiers).toEqual([]);
		});

		it('composes the cumulative per-level bonuses for the player levels against the reference data', async () => {
			// Level-1 and level-2 Strength payouts both apply at level 2; the far-off level-5 does not.
			staticData.proficiencies = [
				proficiency(0, [
					additive(1, EAttribute.Strength, 4),
					additive(2, EAttribute.Strength, 6),
					additive(5, EAttribute.Strength, 100)
				])
			];
			mockFetchSocket.mockResolvedValue([playerProficiency(0, 2)]);
			await playerProficiencies.load();

			expect(playerProficiencies.battleModifiers).toEqual([
				{
					attribute: EAttribute.Strength,
					amount: 4,
					type: EModifierType.Additive,
					source: EAttributeModifierSource.Proficiency
				},
				{
					attribute: EAttribute.Strength,
					amount: 6,
					type: EModifierType.Additive,
					source: EAttributeModifierSource.Proficiency
				}
			]);
		});

		it('skips a player proficiency with no matching reference definition (retired/out-of-range slot)', async () => {
			staticData.proficiencies = [proficiency(0, [additive(1, EAttribute.Strength, 4)])];
			mockFetchSocket.mockResolvedValue([playerProficiency(0, 1), playerProficiency(9, 5)]);
			await playerProficiencies.load();

			expect(playerProficiencies.battleModifiers).toEqual([
				{
					attribute: EAttribute.Strength,
					amount: 4,
					type: EModifierType.Additive,
					source: EAttributeModifierSource.Proficiency
				}
			]);
		});

		it('returns a stable reference while inputs are unchanged and a new one when levels change', async () => {
			staticData.proficiencies = [proficiency(0, [additive(1, EAttribute.Strength, 4)])];
			mockFetchSocket.mockResolvedValue([playerProficiency(0, 1)]);
			await playerProficiencies.load();

			// Same inputs → same reference, so the battle engine's per-spawn change-detection re-arms (#811).
			const first = playerProficiencies.battleModifiers;
			expect(playerProficiencies.battleModifiers).toBe(first);

			// A level change re-derives → a new reference, so the next spawn rebuilds the battler.
			mockFetchSocket.mockResolvedValue([playerProficiency(0, 2)]);
			await playerProficiencies.load(true);
			expect(playerProficiencies.battleModifiers).not.toBe(first);
		});
	});
});
