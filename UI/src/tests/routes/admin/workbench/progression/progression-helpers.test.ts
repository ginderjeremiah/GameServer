import { describe, it, expect } from 'vitest';
import { EActivityKey, EAttribute, EModifierType } from '$lib/api';
import {
	activityKeyGroups,
	cumulativeXp,
	decipherThresholds,
	hasTierCollision,
	isMilestoneLevel,
	modifiersAtLevel,
	newPath,
	newProficiency,
	pathIdentityDto,
	pathWarnings,
	payoutLevels,
	prerequisiteRootWarnings,
	proficiencyBlockingWarnings,
	profIdentityDto,
	proficiencyWarnings,
	renumberTiers,
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

describe('activity-key picker', () => {
	it('groups every key into damage-dealt / combat-events / resistance with no leaks or dupes', () => {
		const keys = (Object.values(EActivityKey).filter((v) => typeof v === 'number') as number[]).sort((a, b) => a - b);
		const grouped = activityKeyGroups.flatMap((g) => g.options.map((o) => o.value)).sort((a, b) => a - b);
		expect(grouped).toEqual(keys);
		expect(activityKeyGroups.map((g) => g.label)).toEqual([
			'Damage dealt',
			'Combat events',
			'Damage taken — resistance'
		]);
		const groupOf = (key: number) => activityKeyGroups.find((g) => g.options.some((o) => o.value === key))?.label;
		expect(groupOf(EActivityKey.Fire)).toBe('Damage dealt');
		expect(groupOf(EActivityKey.Crit)).toBe('Combat events');
		expect(groupOf(EActivityKey.Parry)).toBe('Combat events');
		expect(groupOf(EActivityKey.FireResist)).toBe('Damage taken — resistance');
	});

	it('labels options through the shared activity-key labeller (bare stems in the resist group)', () => {
		const textOf = (key: number) => activityKeyGroups.flatMap((g) => g.options).find((o) => o.value === key)?.text;
		expect(textOf(EActivityKey.Dot)).toBe('DoT');
		expect(textOf(EActivityKey.Crit)).toBe('Critical damage');
		expect(textOf(EActivityKey.DotResist)).toBe('DoT');
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
	it('pathWarnings flags a blank name', () => {
		expect(pathWarnings(newPath(1))).toContain('Missing name');
		expect(pathWarnings({ ...newPath(1), name: 'Fire' })).toHaveLength(0);
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

	it('proficiencyBlockingWarnings flags only the out-of-range payout, not MaxLevel/XP-curve nags (#2222)', () => {
		// Neither condition is enforced by AdminProficiencies/Contracts.Proficiency (confirmed against the
		// backend), so gating Save on them would block a save the backend would actually accept.
		const invalidCurve = tier({ id: 1, maxLevel: 0, baseXp: -1, xpGrowth: 0 });
		expect(proficiencyWarnings(invalidCurve)).toEqual(
			expect.arrayContaining(['Max level must be at least 1', 'XP curve must be positive'])
		);
		expect(proficiencyBlockingWarnings(invalidCurve)).toHaveLength(0);

		const outOfRange = tier({
			id: 2,
			maxLevel: 5,
			levelModifiers: [
				{ level: 8, attributeId: EAttribute.Strength, modifierTypeId: EModifierType.Additive, amount: 1 }
			]
		});
		expect(proficiencyBlockingWarnings(outOfRange)).toEqual(['Modifier level 8 out of range']);

		const rewardOutOfRange = tier({ id: 4, maxLevel: 5, levelRewards: [{ level: 9, rewardSkillId: 1 }] });
		expect(proficiencyBlockingWarnings(rewardOutOfRange)).toEqual(['Reward level 9 out of range']);

		const inRange = tier({ id: 3, maxLevel: 5 });
		expect(proficiencyBlockingWarnings(inRange)).toHaveLength(0);
	});

	it('prerequisiteRootWarnings flags a non-root tier carrying prerequisites (#2275)', () => {
		// AdminProficiencies rejects this shape whenever it's reachable — either directly (SetPrerequisites'
		// ValidatePrerequisiteIds) or through an identity-only ordinal edit that strands an already-gated
		// root tier (FindPrerequisiteRootViolation) — so it must block Save, mirroring the level-range rule.
		const strandedGateway = tier({ id: 1, pathOrdinal: 1, prerequisiteIds: [7] });
		expect(prerequisiteRootWarnings(strandedGateway)).toEqual(['Prerequisites are only allowed on a root tier']);
		expect(proficiencyBlockingWarnings(strandedGateway)).toContain('Prerequisites are only allowed on a root tier');
		expect(proficiencyWarnings(strandedGateway)).toContain('Prerequisites are only allowed on a root tier');

		const rootGateway = tier({ id: 2, pathOrdinal: 0, prerequisiteIds: [7] });
		expect(prerequisiteRootWarnings(rootGateway)).toHaveLength(0);

		const nonRootNoPrereqs = tier({ id: 3, pathOrdinal: 1, prerequisiteIds: [] });
		expect(prerequisiteRootWarnings(nonRootNoPrereqs)).toHaveLength(0);
	});
});

describe('DTO builders', () => {
	it('pathIdentityDto carries the activity key', () => {
		const dto = pathIdentityDto({
			id: 1,
			name: 'Fire',
			description: 'd',
			designerNotes: '',
			activityKey: EActivityKey.Fire
		});
		expect(dto.activityKey).toBe(EActivityKey.Fire);
		expect(dto.name).toBe('Fire');
	});

	it('profIdentityDto clears the child sets (persisted via dedicated relationship endpoints)', () => {
		const dto = profIdentityDto(tier({ id: 1 }));
		expect(dto.levelModifiers).toEqual([]);
		expect(dto.prerequisiteIds).toEqual([]);
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
