import { describe, it, expect } from 'vitest';
import { EDamageType, ERarity, ESkillAcquisition, type IProficiency, type ISkill, type ISkillRecipe } from '$lib/api';
import { buildSynthesis } from '$routes/game/screens/synthesis/synthesis';
import {
	clampScale,
	layoutSynthesisGraph,
	NODE_SIZE,
	ZOOM_MAX,
	ZOOM_MIN,
	zoomAt
} from '$routes/game/screens/synthesis/synthesis-graph';

/* The layout consumes the same `RecipeView[]` the Bench view renders, so the fixtures run real recipes
   through `buildSynthesis` first — the graph and the list can't diverge. Reference catalogues are
   zero-based-id arrays resolved by index, so fixture ids stay contiguous with their array position. */

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

const skills: ISkill[] = [
	skill(0, { name: 'Ember Strike' }),
	skill(1, { name: 'Frost Lance' }),
	skill(2, { name: 'Stone Spear' }),
	skill(3, { name: 'Riptide' }),
	skill(4, { name: 'Frostfire', rarityId: ERarity.Rare }),
	skill(5, { name: 'Maelstrom', rarityId: ERarity.Epic }),
	skill(6, { name: 'Tempest Coil' })
];

const proficiencies: IProficiency[] = [];
const owned = (...ids: number[]) => new Set(ids);
const levels = (entries: [number, number][] = []) => new Map(entries);

const build = (recipes: ISkillRecipe[], ownedIds: Set<number>) =>
	layoutSynthesisGraph(buildSynthesis(recipes, skills, proficiencies, ownedIds, levels()));

describe('layoutSynthesisGraph', () => {
	it('returns an empty layout for no discovered recipes', () => {
		const layout = layoutSynthesisGraph([]);
		expect(layout.nodes).toHaveLength(0);
		expect(layout.edges).toHaveLength(0);
		expect(layout.width).toBe(0);
		expect(layout.height).toBe(0);
	});

	it('lays a single ready recipe out as inputs → fusion → result, left to right', () => {
		const layout = build([recipe(0, 4, [0, 1])], owned(0, 1));

		const inputs = layout.nodes.filter((n) => n.kind === 'input');
		const fusion = layout.nodes.find((n) => n.kind === 'fusion');
		const result = layout.nodes.find((n) => n.kind === 'result');
		expect(inputs).toHaveLength(2);
		expect(fusion).toBeDefined();
		expect(result).toBeDefined();

		// Inputs are leaf skills — not selectable; fusion/result carry the recipe id and are selectable.
		expect(inputs.every((n) => !n.selectable && n.recipeId === undefined)).toBe(true);
		expect(fusion?.selectable).toBe(true);
		expect(fusion?.recipeId).toBe(0);
		expect(result?.selectable).toBe(true);
		expect(result?.recipeId).toBe(0);
		expect(result?.masked).toBe(false);
		expect(result?.skill?.name).toBe('Frostfire');

		// Three edges: each input → fusion, then fusion → result.
		expect(layout.edges).toHaveLength(3);
		expect(layout.edges.filter((e) => e.to === fusion?.key)).toHaveLength(2);
		expect(layout.edges.some((e) => e.from === fusion?.key && e.to === result?.key)).toBe(true);

		// Left-to-right layering: inputs before the fusion before the result.
		const fusionX = fusion?.x ?? 0;
		const resultX = result?.x ?? 0;
		expect(inputs.every((n) => n.x < fusionX)).toBe(true);
		expect(fusionX).toBeLessThan(resultX);

		// Positive, padded extent.
		expect(layout.width).toBeGreaterThan(0);
		expect(layout.height).toBeGreaterThan(0);
		expect(layout.nodes.every((n) => n.x - NODE_SIZE[n.kind].w / 2 >= 0 && n.y - NODE_SIZE[n.kind].h / 2 >= 0)).toBe(
			true
		);
	});

	it('masks a hinted recipe and omits its unowned input', () => {
		// Owns input 0 only — recipe is hinted, its result + the missing input 1 stay sealed.
		const layout = build([recipe(0, 4, [0, 1])], owned(0));

		const inputs = layout.nodes.filter((n) => n.kind === 'input');
		const result = layout.nodes.find((n) => n.kind === 'result');
		const fusion = layout.nodes.find((n) => n.kind === 'fusion');

		// Only the single owned input is a node — the unowned input never appears.
		expect(inputs).toHaveLength(1);
		expect(inputs[0].skill?.name).toBe('Ember Strike');

		// The masked result leaks nothing: no skill, no skill-keyed node, masked flag set.
		expect(result?.masked).toBe(true);
		expect(result?.skill).toBeUndefined();
		expect(result?.key.startsWith('s')).toBe(false);
		expect(fusion?.masked).toBe(true);

		// One input edge (owned) + the fusion → result edge, both carrying the hinted state.
		expect(layout.edges).toHaveLength(2);
		expect(layout.edges.every((e) => e.state === 'hinted')).toBe(true);
	});

	it('chains a synthesized result into a deeper recipe (recipe-graph depth)', () => {
		// A: 0 + 1 → 4 (owned result → done). B: 4 + 2 → 5 (both owned → ready). Skill 4 is A's result *and*
		// B's input, so it is one shared `result` node with an onward edge into B's fusion.
		const recipes = [recipe(0, 4, [0, 1]), recipe(1, 5, [4, 2])];
		const layout = build(recipes, owned(0, 1, 2, 4));

		const shared = layout.nodes.find((n) => n.kind === 'result' && n.recipeId === 0);
		expect(shared).toBeDefined();
		expect(shared?.skill?.name).toBe('Frostfire');
		expect(shared?.key).toBe('s4');

		const fusionB = layout.nodes.find((n) => n.kind === 'fusion' && n.recipeId === 1);
		// The chain edge: A's result feeds B's fusion.
		expect(layout.edges.some((e) => e.from === 's4' && e.to === fusionB?.key)).toBe(true);

		// The shared result sits to the left of B's fusion, which sits left of B's result — increasing depth.
		const resultB = layout.nodes.find((n) => n.kind === 'result' && n.recipeId === 1);
		expect(shared?.x ?? 0).toBeLessThan(fusionB?.x ?? 0);
		expect(fusionB?.x ?? 0).toBeLessThan(resultB?.x ?? 0);
	});

	it('keeps a skill shared across recipes as one node with edges to each fusion', () => {
		// Skill 0 feeds both recipes; it must be a single input node, not duplicated.
		const recipes = [recipe(0, 4, [0, 1]), recipe(1, 5, [0, 2])];
		const layout = build(recipes, owned(0, 1, 2));

		const sharedInput = layout.nodes.filter((n) => n.key === 's0');
		expect(sharedInput).toHaveLength(1);
		expect(layout.edges.filter((e) => e.from === 's0')).toHaveLength(2);
	});
});

describe('pan / zoom helpers', () => {
	it('clamps the zoom scale into the allowed band', () => {
		expect(clampScale(10)).toBe(ZOOM_MAX);
		expect(clampScale(0.01)).toBe(ZOOM_MIN);
		expect(clampScale(1)).toBe(1);
	});

	it('zooms toward the focal point, keeping the world point under the cursor fixed', () => {
		const vp = { x: 0, y: 0, scale: 1 };
		const zoomed = zoomAt(vp, 2, 100, 50);
		expect(zoomed.scale).toBe(2);
		// The world point at the cursor before/after the zoom is identical.
		const worldBefore = { x: (100 - vp.x) / vp.scale, y: (50 - vp.y) / vp.scale };
		const worldAfter = { x: (100 - zoomed.x) / zoomed.scale, y: (50 - zoomed.y) / zoomed.scale };
		expect(worldAfter.x).toBeCloseTo(worldBefore.x);
		expect(worldAfter.y).toBeCloseTo(worldBefore.y);
	});

	it('does not drift the pan when the zoom is clamped', () => {
		const vp = { x: 30, y: -10, scale: ZOOM_MAX };
		// Already at max — zooming in further is a clamped no-op, so the viewport is unchanged.
		expect(zoomAt(vp, 2, 100, 100)).toEqual(vp);
	});
});
