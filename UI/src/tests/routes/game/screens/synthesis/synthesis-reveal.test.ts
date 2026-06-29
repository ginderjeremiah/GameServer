import { describe, it, expect } from 'vitest';
import { EDamageType, ERarity, ESkillAcquisition, type IProficiency, type ISkill, type ISkillRecipe } from '$lib/api';
import {
	buildSynthesis,
	recipeStateAccent,
	representativeRecipe,
	type RecipeState
} from '$routes/game/screens/synthesis/synthesis';

/* The derivation takes explicit args and needs no mocks. The reference catalogues are zero-based-id
   arrays (resolved by index), so the fixtures keep ids contiguous with their array position. */

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

const recipe = (
	id: number,
	resultSkillId: number,
	inputSkillIds: number[],
	conditions: { proficiencyId: number; minLevel: number }[] = [],
	over: Partial<ISkillRecipe> = {}
): ISkillRecipe => ({ id, resultSkillId, inputSkillIds, conditions, ...over });

const prof = (id: number, name: string): IProficiency => ({
	id,
	name,
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
});

/* A small fixed world: skills 0..3 are inputs, skills 4..6 are synthesis results. */
const skills: ISkill[] = [
	skill(0, { name: 'Ember Strike' }),
	skill(1, { name: 'Frost Lance' }),
	skill(2, { name: 'Stone Spear' }),
	skill(3, { name: 'Riptide' }),
	skill(4, { name: 'Frostfire', word: 'sijren', pronunciation: 'sij·ren', translation: 'The Riven Frost' }),
	skill(5, { name: 'Lava Surge', rarityId: ERarity.Rare }),
	skill(6, { name: 'Tempest Coil' })
];

const proficiencies: IProficiency[] = [prof(0, 'Geomancy'), prof(1, 'Pyromancy')];

const owned = (...ids: number[]) => new Set(ids);
const levels = (entries: [number, number][] = []) => new Map(entries);

describe('buildSynthesis — conservative hinted reveal', () => {
	it('hides a recipe the player owns no inputs of', () => {
		const recipes = [recipe(0, 4, [0, 1])];
		const result = buildSynthesis(recipes, skills, proficiencies, owned(), levels());
		expect(result).toHaveLength(0);
	});

	it('marks a partly-owned recipe as hinted and masks the result + missing inputs', () => {
		const recipes = [recipe(0, 4, [0, 1])];
		const [view] = buildSynthesis(recipes, skills, proficiencies, owned(0), levels());

		expect(view.state).toBe<RecipeState>('hinted');
		// Result identity is sealed until every input is owned.
		expect(view.result).toBeUndefined();
		expect(view.ownedInputCount).toBe(1);
		expect(view.inputCount).toBe(2);
		// The owned input resolves; the unowned one is masked (no skill).
		expect(view.inputs[0].owned).toBe(true);
		expect(view.inputs[0].skill?.name).toBe('Ember Strike');
		expect(view.inputs[1].owned).toBe(false);
		expect(view.inputs[1].skill).toBeUndefined();
	});

	it('fully reveals a recipe once every input is owned (ready, no gate)', () => {
		const recipes = [recipe(0, 4, [0, 1])];
		const [view] = buildSynthesis(recipes, skills, proficiencies, owned(0, 1), levels());

		expect(view.state).toBe<RecipeState>('ready');
		expect(view.result?.name).toBe('Frostfire');
		expect(view.result?.word).toBe('sijren');
		expect(view.inputs.every((i) => i.owned)).toBe(true);
	});

	it('reveals but gates a recipe whose proficiency condition is unmet', () => {
		const recipes = [recipe(0, 5, [0, 2], [{ proficiencyId: 0, minLevel: 5 }])];
		const [view] = buildSynthesis(recipes, skills, proficiencies, owned(0, 2), levels([[0, 3]]));

		expect(view.state).toBe<RecipeState>('gated');
		// The result is shown (the spike's "fully shown once you own all inputs"); the gate is the condition.
		expect(view.result?.name).toBe('Lava Surge');
		expect(view.conditions).toHaveLength(1);
		expect(view.conditions[0]).toMatchObject({ name: 'Geomancy', minLevel: 5, currentLevel: 3, met: false });
	});

	it('is ready when every condition is met (a missing proficiency counts as level 0)', () => {
		const recipes = [recipe(0, 5, [0, 2], [{ proficiencyId: 0, minLevel: 5 }])];
		const met = buildSynthesis(recipes, skills, proficiencies, owned(0, 2), levels([[0, 5]]));
		expect(met[0].state).toBe<RecipeState>('ready');
		expect(met[0].conditions[0].met).toBe(true);

		// Without the proficiency row the level reads as 0, so the same recipe is gated.
		const absent = buildSynthesis(recipes, skills, proficiencies, owned(0, 2), levels());
		expect(absent[0].state).toBe<RecipeState>('gated');
		expect(absent[0].conditions[0].currentLevel).toBe(0);
	});

	it('marks a recipe whose result is already owned as done', () => {
		const recipes = [recipe(0, 4, [0, 1])];
		// Owns the result (4) but not the inputs — still done (synthesis is idempotent / one-time).
		const [view] = buildSynthesis(recipes, skills, proficiencies, owned(4), levels());
		expect(view.state).toBe<RecipeState>('done');
		expect(view.result?.name).toBe('Frostfire');
	});

	it('excludes retired recipes (an already-synthesized result persists as a normal skill)', () => {
		const recipes = [recipe(0, 4, [0, 1], [], { retiredAt: '2026-01-01T00:00:00Z' })];
		const result = buildSynthesis(recipes, skills, proficiencies, owned(0, 1), levels());
		expect(result).toHaveLength(0);
	});

	it('drops a recipe whose result skill is missing from the catalogue', () => {
		const recipes = [recipe(0, 99, [0, 1])];
		const result = buildSynthesis(recipes, skills, proficiencies, owned(0, 1), levels());
		expect(result).toHaveLength(0);
	});

	it('orders recipes ready → gated → hinted → done, then by id', () => {
		// Owns 0,1,2,3: recipe 0 (inputs 0,1) is ready; recipe 1 (inputs 0,2, gated) is gated; recipe 2
		// (inputs 3 + unowned 5) is hinted; recipe 3 (result 0 already owned) is done.
		const recipes = [
			recipe(0, 4, [0, 1]),
			recipe(1, 5, [0, 2], [{ proficiencyId: 0, minLevel: 5 }]),
			recipe(2, 6, [3, 5]),
			recipe(3, 0, [0])
		];
		const view = buildSynthesis(recipes, skills, proficiencies, owned(0, 1, 2, 3), levels());
		expect(view.map((r) => r.state)).toEqual<RecipeState[]>(['ready', 'gated', 'hinted', 'done']);
	});
});

describe('representativeRecipe', () => {
	it('returns the first (most actionable) recipe, or undefined when empty', () => {
		const recipes = buildSynthesis([recipe(0, 4, [0, 1])], skills, proficiencies, owned(0, 1), levels());
		expect(representativeRecipe(recipes)?.id).toBe(0);
		expect(representativeRecipe([])).toBeUndefined();
	});
});

describe('recipeStateAccent', () => {
	it('maps each state to a themeable CSS variable', () => {
		expect(recipeStateAccent('ready')).toBe('var(--accent)');
		expect(recipeStateAccent('gated')).toBe('var(--gold)');
		expect(recipeStateAccent('hinted')).toBe('var(--text-muted)');
		expect(recipeStateAccent('done')).toBe('var(--success)');
	});
});
