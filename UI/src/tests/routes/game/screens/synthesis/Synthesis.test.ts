import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent, waitFor } from '@testing-library/svelte';
import { EDamageType, ERarity, ESkillAcquisition, type IProficiency, type ISkill, type ISkillRecipe } from '$lib/api';

/* The screen drives the real playerProficiencies store (fetched over the socket) for the gate state and
   reads recipes/skills/proficiencies from staticData, so the socket fetch + staticData are stubbed; the
   rest of the stores stay real (the $components barrel pulls in the engine, which reads them) so the
   confirm modal + navigation behave like production. Only the socket command sender is stubbed. The owned
   skills come from the real playerManager singleton, set per test. */
const { mockFetchSocket, mockSendCommand, mockToastError, staticData } = vi.hoisted(() => ({
	mockFetchSocket: vi.fn(),
	mockSendCommand: vi.fn(),
	mockToastError: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/api')>();
	return {
		...actual,
		fetchSocketData: mockFetchSocket,
		apiSocket: { ...actual.apiSocket, sendSocketCommand: mockSendCommand }
	};
});
vi.mock('$stores', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$stores')>();
	return { ...actual, staticData, toastError: mockToastError };
});

import Synthesis from '$routes/game/screens/synthesis/Synthesis.svelte';
import { cancelActiveModal, confirmActiveModal, navigation, playerProficiencies } from '$stores';
import { playerManager } from '$lib/engine';

const skill = (id: number, over: Partial<ISkill> = {}): ISkill => ({
	id,
	name: `Skill ${id}`,
	baseDamage: 10,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	damageType: EDamageType.Physical,
	acquisition: ESkillAcquisition.Synthesis,
	...over
});

const SKILLS: ISkill[] = [
	skill(0, { name: 'Ember Strike' }),
	skill(1, { name: 'Frost Lance' }),
	skill(2, { name: 'Stone Spear' }),
	skill(3, { name: 'Frostfire', word: 'sijren', rarityId: ERarity.Rare }),
	skill(4, { name: 'Lava Surge', rarityId: ERarity.Rare }),
	skill(5, { name: 'Tempest Coil' }),
	skill(6, { name: 'Graverend', rarityId: ERarity.Epic })
];

const PROFICIENCIES: IProficiency[] = [
	{
		id: 0,
		name: 'Geomancy',
		description: '',
		iconPath: '',
		word: '',
		pronunciation: '',
		translation: '',
		pathId: 0,
		pathOrdinal: 0,
		maxLevel: 10,
		baseXp: 100,
		xpGrowth: 1,
		levelModifiers: [],
		levelRewards: [],
		prerequisiteIds: []
	}
];

const RECIPES: ISkillRecipe[] = [
	{ id: 0, resultSkillId: 3, inputSkillIds: [0, 1], conditions: [] }, // ready (owns 0,1)
	{ id: 1, resultSkillId: 4, inputSkillIds: [0, 2], conditions: [{ proficiencyId: 0, minLevel: 5 }] }, // gated (level 3)
	{ id: 2, resultSkillId: 6, inputSkillIds: [0, 9], conditions: [] }, // hinted (owns 0, not 9)
	{ id: 3, resultSkillId: 5, inputSkillIds: [0, 1], conditions: [] } // done (owns result 5)
];

beforeEach(() => {
	playerProficiencies.reset();
	staticData.skills = SKILLS;
	staticData.skillRecipes = RECIPES;
	staticData.proficiencies = PROFICIENCIES;
	staticData.attributes = undefined;
	mockToastError.mockClear();
	mockSendCommand.mockReset();
	navigation.clear();
	// Owns inputs 0,1,2 and the result skill 5 (so recipe 3 is "done").
	playerManager.unlockedSkills = [
		{ skillId: 0, selected: false },
		{ skillId: 1, selected: false },
		{ skillId: 2, selected: false },
		{ skillId: 5, selected: false }
	];
	// Geomancy level 3 — below recipe 1's gate of 5.
	mockFetchSocket.mockResolvedValue([{ proficiencyId: 0, level: 3, xp: 0 }]);
});

afterEach(() => cleanup());

describe('Synthesis screen', () => {
	it('fetches gate state and renders the discovered recipes', async () => {
		render(Synthesis);
		expect(screen.getByTestId('synthesis-screen')).toBeTruthy();
		// All four recipes are discovered (the player owns at least one input of each).
		expect(await screen.findByTestId('recipe-0')).toBeTruthy();
		expect(screen.getByTestId('recipe-1')).toBeTruthy();
		expect(screen.getByTestId('recipe-2')).toBeTruthy();
		expect(screen.getByTestId('recipe-3')).toBeTruthy();
		expect(mockFetchSocket).toHaveBeenCalledWith('GetPlayerProficiencies');
		expect(screen.queryByTestId('synthesis-empty')).toBeNull();
	});

	it('opens to the ready recipe — its result revealed and the Synthesize CTA enabled', async () => {
		render(Synthesis);
		await screen.findByTestId('recipe-0');
		// The ready recipe (Frostfire) is the representative selection; the dossier reveals its name + word.
		expect(screen.getAllByText('Frostfire').length).toBeGreaterThan(0);
		const synth = (await screen.findByTestId('synthesize-cta')) as HTMLButtonElement;
		expect(synth.disabled).toBe(false);
	});

	it('shows the gate-locked CTA for an owned-but-gated recipe', async () => {
		render(Synthesis);
		await fireEvent.click(await screen.findByTestId('recipe-1'));
		expect(screen.getByTestId('cta-gated')).toBeTruthy();
		// The unmet Geomancy condition is surfaced as the gate.
		expect(screen.getAllByText(/Geomancy/).length).toBeGreaterThan(0);
	});

	it('keeps a hinted recipe sealed (no result identity leaked)', async () => {
		render(Synthesis);
		await fireEvent.click(await screen.findByTestId('recipe-2'));
		expect(screen.getByTestId('cta-hinted')).toBeTruthy();
		// The hinted result (Graverend) is never named anywhere on the screen.
		expect(screen.queryByText('Graverend')).toBeNull();
	});

	it('shows the empty state for a player who owns no recipe inputs', async () => {
		playerManager.unlockedSkills = [];
		render(Synthesis);
		expect(await screen.findByTestId('synthesis-empty')).toBeTruthy();
		expect(screen.queryByTestId('recipe-0')).toBeNull();
		expect(mockToastError).not.toHaveBeenCalled();
	});

	it('surfaces an error (not the empty state) when the gate fetch fails', async () => {
		mockFetchSocket.mockRejectedValue(new Error('network down'));
		render(Synthesis);
		expect(await screen.findByTestId('synthesis-error')).toBeTruthy();
		expect(screen.queryByTestId('synthesis-empty')).toBeNull();
		expect(mockToastError).toHaveBeenCalledTimes(1);
	});

	it('clicking the CTA confirms then fires SynthesizeSkill for the ready recipe', async () => {
		mockSendCommand.mockResolvedValue({ data: { resultSkillId: 4 } });
		render(Synthesis);
		// The ready recipe (id 0) is the default selection; its CTA is enabled.
		await fireEvent.click(await screen.findByTestId('synthesize-cta'));
		// The confirm dialog is enqueued (one-time, non-consumptive guard) — confirm it.
		confirmActiveModal();
		await waitFor(() => expect(mockSendCommand).toHaveBeenCalledWith('SynthesizeSkill', 0));
		// On success the result unlocks onto the player, flipping the recipe to done.
		await waitFor(() => expect(playerManager.unlockedSkills.some((s) => s.skillId === 4)).toBe(true));
	});

	it('cancelling the confirm dialog does not fire the command', async () => {
		render(Synthesis);
		await fireEvent.click(await screen.findByTestId('synthesize-cta'));
		cancelActiveModal();
		await Promise.resolve();
		expect(mockSendCommand).not.toHaveBeenCalled();
	});

	it('a done recipe routes "View in Skills" through the navigation store', async () => {
		render(Synthesis);
		await fireEvent.click(await screen.findByTestId('recipe-3')); // the done recipe
		await fireEvent.click(screen.getByText('View in Skills'));
		expect(navigation.requestedScreen).toBe('skills');
	});
});
