import { describe, it, expect, beforeEach, vi } from 'vitest';

// The store reads over the socket via fetchSocketData; stub just that export while keeping the real
// IPlayerProficiency / IProficiency types (and the rest of the barrel) intact.
const { mockFetchSocket } = vi.hoisted(() => ({ mockFetchSocket: vi.fn() }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, fetchSocketData: mockFetchSocket };
});

import { EAttribute, EModifierType, type IPlayerProficiency, type IProficiency } from '$lib/api';
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
	iconPath: '',
	pathId: 0,
	pathOrdinal: id,
	maxLevel: 10,
	baseXp: 100,
	xpGrowth: 1,
	startsUnlocked: false,
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
