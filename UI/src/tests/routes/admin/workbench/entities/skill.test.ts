import { describe, it, expect, beforeEach, vi } from 'vitest';
import {
	ERarity,
	EAttribute,
	EChangeType,
	EModifierType,
	ESkillAcquisition,
	ESkillEffectTarget,
	type ISkill
} from '$lib/api';
import type { TableSectionConfig } from '$routes/admin/workbench/entities/types';

/* Skill config transforms: `newItem` defaults, the derived meta line, the effects
   `newRow` defaults, and the persist path — a child-only change (effects or
   multipliers) must NOT hit the identity Add/Edit endpoint, and an untouched
   child collection is skipped. `fetchSocketData`/`ApiRequest` are stubbed; the
   real `persistEntity` orchestration runs unmocked. */

const { staticData, socket, mockPost, mockFetch } = vi.hoisted(() => {
	const socket = { skills: [] as unknown[] };
	return {
		// eslint-disable-next-line @typescript-eslint/no-explicit-any
		staticData: {} as any,
		socket,
		mockPost: vi.fn(),
		mockFetch: vi.fn(async (command: string) => (command === 'GetSkills' ? socket.skills : []))
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

import { skillEntity } from '$routes/admin/workbench/entities/skill';

/** Finds the body posted to a given AdminTools endpoint (or undefined if never called). */
const postBodyTo = (endpoint: string) => mockPost.mock.calls.find((c) => c[0] === endpoint)?.[1];

/** A table section's config by key (for exercising its `newRow` factory). */
const tableSection = (key: string) => skillEntity.sections.find((s) => s.key === key) as TableSectionConfig<ISkill>;

const effect = (over: Partial<ISkill['effects'][number]> = {}): ISkill['effects'][number] => ({
	id: 1,
	target: ESkillEffectTarget.Opponent,
	attributeId: EAttribute.Defense,
	modifierTypeId: EModifierType.Multiplicative,
	amount: 0.5,
	durationMs: 3000,
	scalingAttributeId: EAttribute.Strength,
	scalingAmount: 0,
	...over
});

beforeEach(() => {
	mockPost.mockReset().mockResolvedValue(undefined);
	mockFetch.mockClear();
	socket.skills = [];
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

describe('skillEntity', () => {
	it('newItem defaults to a 10-damage, 2s-cooldown skill with empty collections', () => {
		expect(skillEntity.newItem(4)).toEqual({
			id: 4,
			name: '',
			baseDamage: 10,
			cooldownMs: 2000,
			iconPath: '',
			rarityId: ERarity.Common,
			word: '',
			pronunciation: '',
			translation: '',
			acquisition: ESkillAcquisition.Player,
			description: '',
			damageMultipliers: [],
			effects: []
		});
	});

	it('list badge surfaces the rarity tier name and hue', () => {
		const skill: ISkill = { ...skillEntity.newItem(1), rarityId: ERarity.Legendary };
		expect(skillEntity.listBadge?.(skill)).toBe('Legendary');
		expect(skillEntity.badgeColor?.(skill)).toContain('--rarity-');
	});

	it('the identity section exposes a Rarity select bound to rarityId', () => {
		const identity = skillEntity.sections.find((s) => s.key === 'identity');
		const fields = identity && 'fields' in identity ? identity.fields : [];
		const rarityField = fields.find((f) => f.key === 'rarityId');
		expect(rarityField).toMatchObject({ type: 'select', label: 'Rarity' });
	});

	it('meta shows the damage, multiplier count, effect count and cooldown', () => {
		const skill: ISkill = {
			...skillEntity.newItem(1),
			baseDamage: 25,
			cooldownMs: 1500,
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }],
			effects: [effect()]
		};
		expect(skillEntity.meta(skill)).toEqual([
			['dmg', 25],
			['×mult', 1],
			['fx', 1],
			['cd', '1.5s']
		]);
	});

	it('effects newRow defaults to an opponent Strength additive over 3s, with no scaling', () => {
		expect(tableSection('effects').newRow(skillEntity.newItem(1))).toEqual({
			id: 0,
			target: ESkillEffectTarget.Opponent,
			attributeId: EAttribute.Strength,
			modifierTypeId: EModifierType.Additive,
			amount: 0,
			durationMs: 3000,
			scalingAttributeId: EAttribute.Strength,
			scalingAmount: 0
		});
	});

	it('multipliers newRow picks the first free attribute with a default multiplier of 1', () => {
		// Strength (id 0) is already taken, so the first free attribute (id 1) is chosen.
		const skill: ISkill = {
			...skillEntity.newItem(1),
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 2 }]
		};
		expect(tableSection('multipliers').newRow(skill)).toEqual({ attributeId: EAttribute.Endurance, multiplier: 1 });
	});

	it('section count/warn badges reflect the multiplier and effect collections', () => {
		const multipliers = tableSection('multipliers');
		const effects = tableSection('effects');
		const empty = skillEntity.newItem(1);

		expect(multipliers.count?.(empty)).toBe(0);
		expect(multipliers.warn?.(empty)).toBe('No damage multipliers');
		expect(effects.count?.(empty)).toBe(0);

		const filled: ISkill = {
			...empty,
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }],
			effects: [effect(), effect({ id: 2 })]
		};
		expect(multipliers.count?.(filled)).toBe(1);
		expect(multipliers.warn?.(filled)).toBeNull();
		expect(effects.count?.(filled)).toBe(2);
	});

	it('persist saves the effects when only effects change, without an identity Edit or a multipliers call', async () => {
		const baseline: ISkill = {
			id: 0,
			name: 'Poison Sting',
			baseDamage: 10,
			cooldownMs: 2000,
			iconPath: '',
			rarityId: ERarity.Common,
			word: '',
			pronunciation: '',
			translation: '',
			acquisition: ESkillAcquisition.Player,
			description: 'desc',
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }],
			effects: [effect({ id: 1, amount: 0.5 })]
		};
		const record: ISkill = { ...baseline, effects: [effect({ id: 1, amount: 0.75 })] }; // only the effect amount changed
		socket.skills = [record];

		await skillEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		// Identity + multipliers unchanged → only the effects endpoint is hit, as an Edit.
		expect(postBodyTo('AdminTools/AddEditSkills')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetSkillMultipliers')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetSkillEffects')).toMatchObject({
			id: 0,
			changes: [{ changeType: EChangeType.Edit, item: { id: 1, amount: 0.75 } }]
		});
	});

	it('persist saves the multipliers when only multipliers change, skipping the effects endpoint', async () => {
		const baseline: ISkill = {
			id: 0,
			name: 'Slash',
			baseDamage: 10,
			cooldownMs: 2000,
			iconPath: '',
			rarityId: ERarity.Common,
			word: '',
			pronunciation: '',
			translation: '',
			acquisition: ESkillAcquisition.Player,
			description: 'desc',
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }],
			effects: [effect({ id: 1 })]
		};
		const record: ISkill = { ...baseline, damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 2 }] };
		socket.skills = [record];

		await skillEntity.persist({ added: [], modified: [{ record, baseline }], deleted: [], existingIds: [0] });

		expect(postBodyTo('AdminTools/AddEditSkills')).toBeUndefined();
		expect(postBodyTo('AdminTools/SetSkillMultipliers')).toMatchObject({
			id: 0,
			changes: [{ changeType: EChangeType.Edit, item: { attributeId: EAttribute.Strength, amount: 2 } }]
		});
		// Effects were untouched, so their endpoint is skipped.
		expect(postBodyTo('AdminTools/SetSkillEffects')).toBeUndefined();
	});

	it('persist Adds a new skill and saves its effects against the resolved id', async () => {
		// A freshly-added record has a temporary negative id; persistEntity resolves the real
		// id from the post-save refetch before running the effects child saver.
		const added: ISkill = {
			id: -1,
			name: 'Venom',
			baseDamage: 5,
			cooldownMs: 1000,
			iconPath: '',
			rarityId: ERarity.Common,
			word: '',
			pronunciation: '',
			translation: '',
			acquisition: ESkillAcquisition.Player,
			description: 'Poisons the foe',
			damageMultipliers: [],
			effects: [effect({ id: 0, target: ESkillEffectTarget.Opponent })] // new effect, id 0
		};
		socket.skills = [{ ...added, id: 7 }]; // the persisted record at its real id

		await skillEntity.persist({ added: [added], modified: [], deleted: [], existingIds: [] });

		// Identity Add posted with the child collections stripped.
		const addCall = postBodyTo('AdminTools/AddEditSkills');
		expect(addCall[0].changeType).toBe(EChangeType.Add);
		expect(addCall[0].item).toMatchObject({ id: -1, name: 'Venom', damageMultipliers: [], effects: [] });
		// The new effect is an Add against the resolved id (7).
		expect(postBodyTo('AdminTools/SetSkillEffects')).toMatchObject({
			id: 7,
			changes: [{ changeType: EChangeType.Add, item: { id: 0, target: ESkillEffectTarget.Opponent } }]
		});
	});
});
