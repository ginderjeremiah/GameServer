import { describe, it, expect } from 'vitest';
import { EAttribute, EModifierType } from '$lib/api';
import {
	cumulativeXp,
	decipherThresholds,
	diffCatalogue,
	falloffSteps,
	hasTierCollision,
	homeTierOptions,
	isMilestoneLevel,
	modifiersAtLevel,
	newPath,
	newProficiency,
	pathIdentityDto,
	pathWarnings,
	payoutLevels,
	profIdentityDto,
	proficiencyWarnings,
	renumberTiers,
	resolveId,
	resolveNewIds,
	rewardAtLevel,
	tiersOfPath,
	xpCostCurve
} from '$routes/admin/workbench/progression/progression-helpers';
import type { WorkbenchProficiency } from '$routes/admin/workbench/progression/types';

const tier = (overrides: Partial<WorkbenchProficiency> & Pick<WorkbenchProficiency, 'id'>): WorkbenchProficiency => ({
	...newProficiency(overrides.id, overrides.pathId ?? 1, overrides.pathOrdinal ?? 0),
	...overrides
});

describe('xpCostCurve & cumulativeXp', () => {
	it('computes baseXp × growth^(n-1) per level, rounded', () => {
		expect(xpCostCurve(100, 1.5, 3)).toEqual([100, 150, 225]);
	});

	it('returns an empty curve for a non-positive max level', () => {
		expect(xpCostCurve(100, 1.5, 0)).toEqual([]);
	});

	it('cumulative xp to reach a level is the sum of all lower-level costs', () => {
		// reach lv1 = 0 (nothing below); reach lv3 = cost(1)+cost(2) = 100+150
		expect(cumulativeXp(100, 1.5, 1)).toBe(0);
		expect(cumulativeXp(100, 1.5, 3)).toBe(250);
	});
});

describe('decipherThresholds', () => {
	it('reveals pronunciation at ceil(maxLevel/2) and translation at maxLevel', () => {
		expect(decipherThresholds(10)).toEqual({ pronunciation: 5, translation: 10 });
		expect(decipherThresholds(7)).toEqual({ pronunciation: 4, translation: 7 });
	});

	it('clamps a zero/negative max level to 1', () => {
		expect(decipherThresholds(0)).toEqual({ pronunciation: 1, translation: 1 });
	});
});

describe('falloffSteps', () => {
	it('decays geometrically with distance, starting at 1', () => {
		const steps = falloffSteps(0.5);
		expect(steps.map((s) => s.distance)).toEqual([0, 1, 2]);
		expect(steps[0].factor).toBe(1);
		expect(steps[1].factor).toBeCloseTo(0.5);
		expect(steps[2].factor).toBeCloseTo(0.25);
	});
});

describe('tier layout', () => {
	it('tiersOfPath filters by path and sorts by ordinal', () => {
		const profs = [
			tier({ id: 1, pathId: 1, pathOrdinal: 2 }),
			tier({ id: 2, pathId: 2, pathOrdinal: 0 }),
			tier({ id: 3, pathId: 1, pathOrdinal: 0 })
		];
		expect(tiersOfPath(profs, 1).map((t) => t.id)).toEqual([3, 1]);
	});

	it('renumberTiers assigns contiguous 0..n-1 ordinals in list order', () => {
		const reordered = renumberTiers([tier({ id: 3, pathOrdinal: 5 }), tier({ id: 1, pathOrdinal: 9 })]);
		expect(reordered.map((t) => [t.id, t.pathOrdinal])).toEqual([
			[3, 0],
			[1, 1]
		]);
	});

	it('hasTierCollision detects a duplicate ordinal', () => {
		expect(hasTierCollision([tier({ id: 1, pathOrdinal: 0 }), tier({ id: 2, pathOrdinal: 1 })])).toBe(false);
		expect(hasTierCollision([tier({ id: 1, pathOrdinal: 0 }), tier({ id: 2, pathOrdinal: 0 })])).toBe(true);
	});

	it('homeTierOptions emits one option per tier ordinal', () => {
		const opts = homeTierOptions([
			tier({ id: 1, pathOrdinal: 0, name: 'Fire' }),
			tier({ id: 2, pathOrdinal: 1, name: 'Inferno' })
		]);
		expect(opts).toEqual([
			{ value: 0, text: 'T0 · Fire' },
			{ value: 1, text: 'T1 · Inferno' }
		]);
	});
});

describe('milestone projection', () => {
	const prof = tier({
		id: 1,
		levelModifiers: [
			{ level: 2, attributeId: EAttribute.Strength, modifierTypeId: EModifierType.Additive, amount: 5 },
			{ level: 5, attributeId: EAttribute.Strength, modifierTypeId: EModifierType.Additive, amount: 15 }
		],
		levelRewards: [{ level: 5, rewardSkillId: 9 }]
	});

	it('payoutLevels unions modifier and reward levels, ascending', () => {
		expect(payoutLevels(prof)).toEqual([2, 5]);
	});

	it('modifiersAtLevel / rewardAtLevel / isMilestoneLevel read the right level', () => {
		expect(modifiersAtLevel(prof, 2)).toHaveLength(1);
		expect(rewardAtLevel(prof, 5)?.rewardSkillId).toBe(9);
		expect(rewardAtLevel(prof, 2)).toBeUndefined();
		expect(isMilestoneLevel(prof, 5)).toBe(true);
		expect(isMilestoneLevel(prof, 2)).toBe(false);
	});
});

describe('validation', () => {
	it('pathWarnings flags a blank name and non-positive falloff', () => {
		expect(pathWarnings(newPath(1))).toContain('Missing name');
		expect(pathWarnings({ ...newPath(1), name: 'Fire', falloffBase: 0 })).toContain(
			'Falloff base must be greater than zero'
		);
		expect(pathWarnings({ ...newPath(1), name: 'Fire', falloffBase: 0.6 })).toHaveLength(0);
	});

	it('proficiencyWarnings flags missing words of power and out-of-range levels', () => {
		const blank = tier({ id: 1 });
		expect(proficiencyWarnings(blank)).toEqual(
			expect.arrayContaining(['Missing name', 'Missing words of power', 'Missing icon path'])
		);

		const ranged = tier({
			id: 2,
			name: 'Fire',
			word: 'fyr',
			pronunciation: 'fyoor',
			translation: 'Fire',
			iconPath: 'fire.png',
			maxLevel: 5,
			levelRewards: [{ level: 9, rewardSkillId: 1 }]
		});
		expect(proficiencyWarnings(ranged)).toContain('Reward level 9 out of range');
	});
});

describe('diffCatalogue', () => {
	it('splits into added (no baseline) and modified (differs)', () => {
		const a = { id: 1, name: 'A' };
		const b = { id: 2, name: 'B' };
		const current = [
			{ id: 1, name: 'A2' }, // modified
			{ id: -1, name: 'New' }, // added
			b // unchanged
		];
		const diff = diffCatalogue(current, [a, b]);
		expect(diff.added.map((r) => r.id)).toEqual([-1]);
		expect(diff.modified.map((m) => m.record.id)).toEqual([1]);
	});
});

describe('resolveNewIds & resolveId', () => {
	it('maps added local ids to the persisted ids absent before the save, in send order', () => {
		const added = [{ id: -1 }, { id: -2 }];
		const fresh = [{ id: 0 }, { id: 1 }, { id: 2 }]; // 0 existed; 1,2 are new
		const map = resolveNewIds(fresh, [0], added);
		expect(map.get(-1)).toBe(1);
		expect(map.get(-2)).toBe(2);
	});

	it('resolveId passes through an already-persisted id', () => {
		const map = new Map([[-1, 7]]);
		expect(resolveId(-1, map)).toBe(7);
		expect(resolveId(3, map)).toBe(3);
	});
});

describe('DTO builders', () => {
	it('pathIdentityDto strips contributions to the empty child set', () => {
		const dto = pathIdentityDto({
			id: 1,
			name: 'Fire',
			description: 'd',
			falloffBase: 0.6,
			contributions: [{ skillId: 1, homeTier: 0, weight: 1 }]
		});
		expect(dto.contributions).toEqual([]);
		expect(dto.name).toBe('Fire');
	});

	it('profIdentityDto clears the child collections (persisted through their own setters)', () => {
		const dto = profIdentityDto(tier({ id: 1, levelRewards: [{ level: 5, rewardSkillId: 9 }] }));
		expect(dto.levelModifiers).toEqual([]);
		expect(dto.levelRewards).toEqual([]);
	});
});

describe('factories', () => {
	it('newProficiency carries strawman cap/curve defaults', () => {
		const p = newProficiency(-1, 3, 2);
		expect(p).toMatchObject({
			pathId: 3,
			pathOrdinal: 2,
			maxLevel: 10,
			baseXp: 100,
			xpGrowth: 1.4
		});
	});
});
