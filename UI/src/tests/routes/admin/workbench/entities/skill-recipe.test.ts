import { describe, it, expect, beforeEach, vi } from 'vitest';
import { EChangeType, ESkillAcquisition, type ISkillRecipe } from '$lib/api';
import type {
	ChipsSectionConfig,
	FieldsSectionConfig,
	TableSectionConfig
} from '$routes/admin/workbench/entities/types';

/* Skill-recipe config transforms: `newItem` defaults, the derived title/headline/meta (a recipe is
   nameless — identified by its result skill), the conditions `newRow`, the per-section warn predicates,
   and the persist path — the identity save carries only the result skill while the input skills and
   proficiency conditions go through their own setters, an untouched child collection is skipped, and a
   child-only change must not hit the identity Add/Edit endpoint. `fetchSocketData`/`ApiRequest` are
   stubbed; the real `persistEntity` orchestration runs unmocked. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { recipes: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => (command === 'GetSkillRecipes' ? socket.recipes : []))
	};
});

vi.mock('$stores', () => ({ staticData }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	class ApiRequest {
		static post = mockPost;
		static get = vi.fn();
	}
	return { ...actual, ApiRequest, fetchSocketData: mockFetch };
});

import { skillRecipeEntity } from '$routes/admin/workbench/entities/skill-recipe';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

const section = <K extends string>(key: K) => skillRecipeEntity.sections.find((s) => s.key === key);
const fieldsSection = (key: string) => section(key) as FieldsSectionConfig<ISkillRecipe>;
const chipsSection = (key: string) => section(key) as ChipsSectionConfig<ISkillRecipe>;
const tableSection = (key: string) => section(key) as TableSectionConfig<ISkillRecipe>;

/** A skill reference record at its zero-based slot. `synth`/`retired` shape the recipe authoring rules. */
const skill = (id: number, name: string, opts: { synth?: boolean; retired?: boolean; baseDamage?: number } = {}) => ({
	id,
	name,
	baseDamage: opts.baseDamage ?? 10,
	acquisition: opts.synth ? ESkillAcquisition.Player | ESkillAcquisition.Synthesis : ESkillAcquisition.Player,
	retiredAt: opts.retired ? '2026-01-01T00:00:00Z' : null
});

const recipe = (over: Partial<ISkillRecipe> = {}): ISkillRecipe => ({
	id: 0,
	resultSkillId: 3,
	inputSkillIds: [0, 1],
	conditions: [],
	...over
});

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.recipes = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
	// Skills 0/1 are inputs, 3 is a Synthesis-flagged result; 4 is a non-Synthesis skill; 5 is retired.
	staticData.skills = [
		skill(0, 'Ember'),
		skill(1, 'Stone'),
		skill(2, 'Gust'),
		skill(3, 'Lava', { synth: true }),
		skill(4, 'Punch'),
		skill(5, 'Old Flame', { synth: true, retired: true })
	];
	staticData.proficiencies = [
		{ id: 0, name: 'Fire', retiredAt: null },
		{ id: 1, name: 'Earth', retiredAt: null }
	];
});

describe('skillRecipeEntity', () => {
	it('newItem defaults to the first Synthesis result with empty input/condition collections', () => {
		expect(skillRecipeEntity.newItem(7)).toEqual({ id: 7, resultSkillId: 3, inputSkillIds: [], conditions: [] });
	});

	it('newItem falls back to -1 when no Synthesis-flagged skill is authorable', () => {
		staticData.skills = [skill(0, 'Ember'), skill(4, 'Punch')]; // none flagged Synthesis
		expect(skillRecipeEntity.newItem(7).resultSkillId).toBe(-1);
	});

	it('title and headline derive from the result skill and inputs (the recipe has no name)', () => {
		expect(skillRecipeEntity.title?.(recipe())).toBe('Lava');
		expect(skillRecipeEntity.headline?.(recipe())).toBe('Ember + Stone → Lava');
		expect(skillRecipeEntity.headline?.(recipe({ inputSkillIds: [] }))).toBe('(no inputs) → Lava');
	});

	it('meta shows the input and condition counts', () => {
		expect(
			skillRecipeEntity.meta(recipe({ inputSkillIds: [0, 1], conditions: [{ proficiencyId: 0, minLevel: 2 }] }))
		).toEqual([
			['inputs', 2],
			['cond', 1]
		]);
	});

	it('the result picker offers only live Synthesis skills, keeping a retired current value visible', () => {
		const options = fieldsSection('result').fields[0].options;
		// Active Synthesis result only (Lava); the non-Synthesis Punch and retired Old Flame are excluded.
		expect(options?.()).toEqual([{ value: 3, text: 'Lava' }]);
		// The current value, even retired, stays selectable (marked retired) so an authored result isn't dropped.
		expect(options?.(5)).toEqual([
			{ value: 3, text: 'Lava' },
			{ value: 5, text: 'Old Flame · retired' }
		]);
	});

	it('the inputs catalogue blocks adding a retired skill but keeps it visible', () => {
		const catalogue = chipsSection('inputs').catalogue();
		expect(catalogue.find((c) => c.id === 0)).toMatchObject({ name: 'Ember', addable: true });
		expect(catalogue.find((c) => c.id === 5)).toMatchObject({ name: 'Old Flame', addable: false });
	});

	it('conditions newRow picks the first free proficiency at min level 1', () => {
		expect(tableSection('conditions').newRow(recipe({ conditions: [] }))).toEqual({ proficiencyId: 0, minLevel: 1 });
		expect(tableSection('conditions').newRow(recipe({ conditions: [{ proficiencyId: 0, minLevel: 1 }] }))).toEqual({
			proficiencyId: 1,
			minLevel: 1
		});
	});

	it('warns when the result is not a live Synthesis skill', () => {
		const warn = fieldsSection('result').warn;
		expect(warn?.(recipe({ resultSkillId: 3 }))).toBeNull();
		expect(warn?.(recipe({ resultSkillId: 4 }))).toBe('Result must be a live Synthesis-flagged skill'); // not flagged
		expect(warn?.(recipe({ resultSkillId: 5 }))).toBe('Result must be a live Synthesis-flagged skill'); // retired
	});

	it('warns on no inputs or an input equal to the result', () => {
		const warn = chipsSection('inputs').warn;
		expect(warn?.(recipe({ inputSkillIds: [0, 1] }))).toBeNull();
		expect(warn?.(recipe({ inputSkillIds: [] }))).toBe('No input skills');
		expect(warn?.(recipe({ resultSkillId: 3, inputSkillIds: [0, 3] }))).toBe('An input cannot be the result skill');
	});

	it('warns when a condition level is below 1', () => {
		const warn = tableSection('conditions').warn;
		expect(warn?.(recipe({ conditions: [{ proficiencyId: 0, minLevel: 1 }] }))).toBeNull();
		expect(warn?.(recipe({ conditions: [{ proficiencyId: 0, minLevel: 0 }] }))).toBe(
			'Condition level must be at least 1'
		);
	});

	it('persist saves the inputs when only inputs change, without an identity Edit or a conditions call', async () => {
		const baseline = recipe({ id: 0, inputSkillIds: [0, 1], conditions: [{ proficiencyId: 0, minLevel: 2 }] });
		const record = recipe({ id: 0, inputSkillIds: [0, 2], conditions: [{ proficiencyId: 0, minLevel: 2 }] });
		socket.recipes = [record];

		await skillRecipeEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		expect(postBodyTo('AdminTools/AddEditSkillRecipes')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetSkillRecipeInputs')).toEqual({ id: 0, skillIds: [0, 2] });
		expect(postBodyTo('AdminTools/SetSkillRecipeConditions')).toBeUndefined();
	});

	it('persist saves the conditions when only conditions change, skipping the inputs endpoint', async () => {
		const baseline = recipe({ id: 0, inputSkillIds: [0, 1], conditions: [] });
		const record = recipe({ id: 0, inputSkillIds: [0, 1], conditions: [{ proficiencyId: 1, minLevel: 3 }] });
		socket.recipes = [record];

		await skillRecipeEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		expect(postBodyTo('AdminTools/AddEditSkillRecipes')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetSkillRecipeConditions')).toEqual({
			id: 0,
			conditions: [{ proficiencyId: 1, minLevel: 3 }]
		});
		expect(postBodyTo('AdminTools/SetSkillRecipeInputs')).toBeUndefined();
	});

	it('persist sends an identity Edit (child collections stripped) when the result skill changes', async () => {
		const baseline = recipe({ id: 0, resultSkillId: 3, inputSkillIds: [0, 1], conditions: [] });
		const record = recipe({ id: 0, resultSkillId: 5, inputSkillIds: [0, 1], conditions: [] });
		socket.recipes = [record];

		await skillRecipeEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		const edit = postBodyTo('AdminTools/AddEditSkillRecipes');
		expect(edit[0].changeType).toBe(EChangeType.Edit);
		expect(edit[0].item).toEqual({ id: 0, resultSkillId: 5, retiredAt: undefined, inputSkillIds: [], conditions: [] });
		// Inputs/conditions didn't change, so their setters are skipped.
		expect(postBodyTo('AdminTools/SetSkillRecipeInputs')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetSkillRecipeConditions')).toBeUndefined();
	});

	it('persist Adds a new recipe and saves its inputs/conditions against the resolved id', async () => {
		const added = recipe({
			id: -1,
			resultSkillId: 3,
			inputSkillIds: [0, 1],
			conditions: [{ proficiencyId: 0, minLevel: 2 }]
		});
		socket.recipes = [{ ...added, id: 9 }]; // the persisted record at its real id

		await skillRecipeEntity.persist({ added: [added], modified: [], deleted: [], existingIds: [] });

		const add = postBodyTo('AdminTools/AddEditSkillRecipes');
		expect(add[0].changeType).toBe(EChangeType.Add);
		expect(add[0].item).toMatchObject({ id: -1, resultSkillId: 3, inputSkillIds: [], conditions: [] });
		// The child setters target the resolved real id (9), not the temporary -1.
		expect(postBodyTo('AdminTools/SetSkillRecipeInputs')).toEqual({ id: 9, skillIds: [0, 1] });
		expect(postBodyTo('AdminTools/SetSkillRecipeConditions')).toEqual({
			id: 9,
			conditions: [{ proficiencyId: 0, minLevel: 2 }]
		});
	});
});
