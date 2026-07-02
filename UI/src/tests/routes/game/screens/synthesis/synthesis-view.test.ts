import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EDamageType, ERarity, ESkillAcquisition, type IProficiency, type ISkill, type ISkillRecipe } from '$lib/api';
import type { RecipeView } from '$routes/game/screens/synthesis/synthesis';

/* The view-model reads the live stores + engine + socket, so those are mocked with mutable stand-ins the
   tests populate before constructing the view (mirroring the skills/proficiencies view tests). The real
   $lib/api types stay intact — only `apiSocket` is replaced. */
const { mockPlayerManager, mockPlayerProficiencies, staticData, sendSocketCommand, toastError } = vi.hoisted(() => ({
	mockPlayerManager: {
		unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
		addUnlockedSkill: vi.fn()
	},
	mockPlayerProficiencies: {
		all: [] as { proficiencyId: number; level: number; xp: number }[],
		error: false,
		load: vi.fn()
	},
	staticData: {
		skillRecipes: [] as ISkillRecipe[] | undefined,
		skills: [] as ISkill[] | undefined,
		proficiencies: [] as IProficiency[] | undefined
	},
	sendSocketCommand: vi.fn(),
	toastError: vi.fn()
}));

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager }));
vi.mock('$stores', () => ({ playerProficiencies: mockPlayerProficiencies, staticData, toastError }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand } };
});

import { SynthesisView } from '$routes/game/screens/synthesis/synthesis-view.svelte';

const skill = (id: number, over: Partial<ISkill> = {}): ISkill => ({
	id,
	name: `Skill ${id}`,
	baseDamage: 10,
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	description: '',
	designerNotes: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	acquisition: ESkillAcquisition.Synthesis,
	...over
});

/** A minimal `ready` recipe view for the synthesize-action tests. */
const readyRecipe = (over: Partial<RecipeView> = {}): RecipeView => ({
	id: 7,
	state: 'ready',
	inputs: [
		{ skillId: 0, owned: true, skill: skill(0) },
		{ skillId: 1, owned: true, skill: skill(1) }
	],
	ownedInputCount: 2,
	inputCount: 2,
	conditions: [],
	result: skill(4, { name: 'Frostfire' }),
	...over
});

beforeEach(() => {
	mockPlayerManager.unlockedSkills = [];
	mockPlayerManager.addUnlockedSkill.mockReset();
	mockPlayerProficiencies.all = [];
	mockPlayerProficiencies.error = false;
	staticData.skillRecipes = [];
	staticData.skills = [];
	staticData.proficiencies = [];
	sendSocketCommand.mockReset();
	toastError.mockReset();
});

describe('SynthesisView — derivation from stores', () => {
	beforeEach(() => {
		staticData.skills = [
			skill(0, { name: 'Ember Strike' }),
			skill(1, { name: 'Frost Lance' }),
			skill(2, { name: 'Frostfire' })
		];
		staticData.proficiencies = [];
		staticData.skillRecipes = [{ id: 0, resultSkillId: 2, inputSkillIds: [0, 1], conditions: [], designerNotes: '' }];
	});

	it('derives recipes, counts and the default selection from the owned skills', () => {
		mockPlayerManager.unlockedSkills = [{ skillId: 0, selected: false }]; // owns one input → hinted
		const view = new SynthesisView();

		expect(view.discoveredCount).toBe(1);
		expect(view.counts.hinted).toBe(1);
		expect(view.isEmpty).toBe(false);
		expect(view.selected?.id).toBe(0);
	});

	it('filters the visible list by state', () => {
		mockPlayerManager.unlockedSkills = [{ skillId: 0, selected: false }];
		const view = new SynthesisView();

		view.setFilter('ready');
		expect(view.visibleRecipes).toHaveLength(0);
		view.setFilter('hinted');
		expect(view.visibleRecipes).toHaveLength(1);
	});

	it('is empty when the player owns no inputs', () => {
		mockPlayerManager.unlockedSkills = [];
		const view = new SynthesisView();
		expect(view.isEmpty).toBe(true);
	});

	it('switches between bench and web views and derives the recipe graph from the same recipes', () => {
		mockPlayerManager.unlockedSkills = [{ skillId: 0, selected: false }]; // owns one input → hinted
		const view = new SynthesisView();

		expect(view.mode).toBe('bench');
		view.setMode('web');
		expect(view.mode).toBe('web');

		// The graph mirrors the discovered recipes: the hinted recipe contributes a fusion + a masked result.
		expect(view.graph.nodes.length).toBeGreaterThan(0);
		expect(view.graph.nodes.some((n) => n.kind === 'fusion' && n.recipeId === 0)).toBe(true);
		expect(view.graph.nodes.some((n) => n.kind === 'result' && n.masked)).toBe(true);
	});
});

describe('SynthesisView.synthesize — the action path', () => {
	it('sends SynthesizeSkill and unlocks the result on success', async () => {
		sendSocketCommand.mockResolvedValue({ data: { resultSkillId: 4 } });
		const view = new SynthesisView();

		const ok = await view.synthesize(readyRecipe());

		expect(ok).toBe(true);
		expect(sendSocketCommand).toHaveBeenCalledWith('SynthesizeSkill', 7);
		expect(mockPlayerManager.addUnlockedSkill).toHaveBeenCalledWith(4);
		expect(toastError).not.toHaveBeenCalled();
		expect(view.synthesizing).toBe(false);
	});

	it('toasts and does not unlock when the command errors', async () => {
		sendSocketCommand.mockResolvedValue({ error: 'nope' });
		const view = new SynthesisView();

		const ok = await view.synthesize(readyRecipe());

		expect(ok).toBe(false);
		expect(mockPlayerManager.addUnlockedSkill).not.toHaveBeenCalled();
		expect(toastError).toHaveBeenCalledOnce();
	});

	it('toasts when the response carries no result skill id', async () => {
		sendSocketCommand.mockResolvedValue({ data: {} });
		const view = new SynthesisView();

		const ok = await view.synthesize(readyRecipe());

		expect(ok).toBe(false);
		expect(mockPlayerManager.addUnlockedSkill).not.toHaveBeenCalled();
		expect(toastError).toHaveBeenCalledOnce();
	});

	it('is a no-op for a recipe that is not ready (anti-cheat is server-side, but the UI never offers it)', async () => {
		const view = new SynthesisView();
		for (const state of ['gated', 'hinted', 'done'] as const) {
			const ok = await view.synthesize(readyRecipe({ state }));
			expect(ok).toBe(false);
		}
		expect(sendSocketCommand).not.toHaveBeenCalled();
	});

	it('guards against a concurrent double-submit', async () => {
		let resolve!: (value: { data: { resultSkillId: number } }) => void;
		sendSocketCommand.mockReturnValue(new Promise((r) => (resolve = r)));
		const view = new SynthesisView();

		const first = view.synthesize(readyRecipe());
		expect(view.synthesizing).toBe(true);
		const second = await view.synthesize(readyRecipe()); // rejected while the first is in flight
		expect(second).toBe(false);
		expect(sendSocketCommand).toHaveBeenCalledOnce();

		resolve({ data: { resultSkillId: 4 } });
		await first;
		expect(view.synthesizing).toBe(false);
	});
});
