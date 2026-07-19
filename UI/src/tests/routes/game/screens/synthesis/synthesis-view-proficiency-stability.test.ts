import { describe, it, expect, vi, beforeEach } from 'vitest';

/* Unlike synthesis-view.test.ts, this file deliberately does NOT mock $stores: proficiencyLevels'
   reference-stability fix (#2136) only exercises Svelte's derived-chain memoization when its
   `playerProficiencies.all` dependency is a real `$state`-backed signal — a plain mock object's
   property mutation isn't tracked, so it could never expose a regression here. playerManager and
   apiSocket are still mocked; neither is exercised. */
const { mockPlayerManager } = vi.hoisted(() => ({
	mockPlayerManager: { unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[] }
}));
vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand: vi.fn() } };
});

import { playerProficiencies, staticData } from '$stores';
import type { IProficiencyXpResultModel } from '$lib/api';
import { SynthesisView } from '$routes/game/screens/synthesis/synthesis-view.svelte';

const xpResult = (proficiencyId: number, newLevel: number, newXp: number): IProficiencyXpResultModel => ({
	proficiencyId,
	xpGained: 10,
	newLevel,
	newXp,
	milestonesCrossed: [],
	grantedSkillIds: []
});

beforeEach(() => {
	mockPlayerManager.unlockedSkills = [];
	playerProficiencies.reset();
	staticData.reset();
	staticData.skillRecipes = [];
	staticData.skills = [];
	staticData.proficiencies = [];
});

describe('SynthesisView.proficiencyLevels — reference stability', () => {
	it('keeps the same Map reference across a push that only moves xp, not level', () => {
		playerProficiencies.applyXpGained({ proficiencies: [xpResult(0, 3, 10)], opened: [] });
		const view = new SynthesisView();

		const before = view.proficiencyLevels;
		expect(before.get(0)).toBe(3);

		// A same-level push still reassigns playerProficiencies.all wholesale (by design).
		playerProficiencies.applyXpGained({ proficiencies: [xpResult(0, 3, 45)], opened: [] });

		expect(view.proficiencyLevels).toBe(before);
	});

	it('returns a new Map when a level actually changes', () => {
		playerProficiencies.applyXpGained({ proficiencies: [xpResult(0, 3, 10)], opened: [] });
		const view = new SynthesisView();
		const before = view.proficiencyLevels;

		playerProficiencies.applyXpGained({ proficiencies: [xpResult(0, 4, 0)], opened: [] });

		expect(view.proficiencyLevels).not.toBe(before);
		expect(view.proficiencyLevels.get(0)).toBe(4);
	});

	it('returns a new Map when a proficiency is newly opened', () => {
		const view = new SynthesisView();
		const before = view.proficiencyLevels;
		expect(before.size).toBe(0);

		playerProficiencies.applyXpGained({ proficiencies: [], opened: [{ proficiencyId: 2 }] });

		expect(view.proficiencyLevels).not.toBe(before);
		expect(view.proficiencyLevels.get(2)).toBe(0);
	});
});
