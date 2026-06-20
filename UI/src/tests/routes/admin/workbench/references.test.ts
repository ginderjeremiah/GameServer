import { describe, it, expect } from 'vitest';
import { EChallengeType, EEntityType, type IChallenge, type IEnemy, type IZone } from '$lib/api';
import {
	computeReferences,
	formatReferenceBody,
	type ReferenceGroup,
	type ReferenceSources
} from '$routes/admin/workbench/references';

const enemy = (id: number, name: string, opts: Partial<IEnemy> = {}): IEnemy => ({
	id,
	name,
	isBoss: false,
	attributeDistribution: [],
	skillPool: [],
	spawns: [],
	...opts
});

const zone = (id: number, name: string, opts: Partial<IZone> = {}): IZone => ({
	id,
	name,
	description: '',
	order: 0,
	levelMin: 1,
	levelMax: 10,
	bossLevel: 1,
	...opts
});

const challenge = (id: number, name: string, opts: Partial<IChallenge> = {}): IChallenge => ({
	id,
	name,
	description: '',
	challengeTypeId: EChallengeType.EnemiesKilled,
	entityType: EEntityType.None,
	progressGoal: 10,
	...opts
});

// Id-aligned reference sets shared across the computation cases.
const sources = (): ReferenceSources => ({
	enemies: [
		enemy(0, 'Cave Bat', {
			spawns: [
				{ zoneId: 0, weight: 5 },
				{ zoneId: 1, weight: 3 }
			],
			skillPool: [0, 1]
		}),
		enemy(1, 'Catacomb Lich', { isBoss: true, spawns: [{ zoneId: 1, weight: 9 }], skillPool: [2] }),
		enemy(2, 'Ancient Wyrm', { isBoss: true, spawns: [], skillPool: [] })
	],
	zones: [
		zone(0, 'Verdant Hollow'),
		zone(1, 'Frost Cavern', { bossEnemyId: 1, unlockChallengeId: 0 }),
		zone(2, 'Ember Wastes', { bossEnemyId: 2 })
	],
	challenges: [
		challenge(0, 'First Steps', { rewardItemId: 0 }),
		challenge(1, 'Bat Hunter', {
			entityType: EEntityType.Enemy,
			targetEntityId: 0,
			rewardItemModId: 1,
			rewardSkillId: 0
		}),
		challenge(2, 'Zone Clearer', { entityType: EEntityType.Zone, targetEntityId: 1 }),
		challenge(3, 'Skill Spammer', { entityType: EEntityType.Skill, targetEntityId: 1, rewardSkillId: 1 })
	]
});

describe('computeReferences — enemies', () => {
	it('lists spawn-table membership and challenge targets, with the enemy-specific spawn note flag', () => {
		expect(computeReferences('enemies', 0, sources())).toEqual([
			{ kind: 'spawnsIn', names: ['Verdant Hollow', 'Frost Cavern'] },
			{ kind: 'challengeTarget', names: ['Bat Hunter'] }
		]);
	});

	it('lists the zones an enemy is the authored boss of, alongside its own spawns', () => {
		expect(computeReferences('enemies', 1, sources())).toEqual([
			{ kind: 'bossOf', names: ['Frost Cavern'] },
			{ kind: 'spawnsIn', names: ['Frost Cavern'] }
		]);
	});

	it('reports only the boss assignment for an enemy with no spawns or challenge targets', () => {
		expect(computeReferences('enemies', 2, sources())).toEqual([{ kind: 'bossOf', names: ['Ember Wastes'] }]);
	});
});

describe('computeReferences — zones', () => {
	it('lists the enemies that spawn in the zone and the challenges that target it', () => {
		expect(computeReferences('zones', 1, sources())).toEqual([
			{ kind: 'spawnedBy', names: ['Cave Bat', 'Catacomb Lich'] },
			{ kind: 'challengeTarget', names: ['Zone Clearer'] }
		]);
	});

	it('reports only the spawning enemies when nothing targets the zone', () => {
		expect(computeReferences('zones', 0, sources())).toEqual([{ kind: 'spawnedBy', names: ['Cave Bat'] }]);
	});
});

describe('computeReferences — challenges', () => {
	it('flags a challenge used as a zone unlock gate as a strong reference', () => {
		expect(computeReferences('challenges', 0, sources())).toEqual([
			{ kind: 'unlockGate', names: ['Frost Cavern'], strong: true }
		]);
	});

	it('returns nothing for a challenge that no zone gates', () => {
		expect(computeReferences('challenges', 1, sources())).toEqual([]);
	});
});

describe('computeReferences — items and item mods', () => {
	it('lists the challenges that reward an item', () => {
		expect(computeReferences('items', 0, sources())).toEqual([{ kind: 'challengeReward', names: ['First Steps'] }]);
	});

	it('lists the challenges that reward an item mod', () => {
		expect(computeReferences('itemMods', 1, sources())).toEqual([{ kind: 'challengeReward', names: ['Bat Hunter'] }]);
	});

	it('returns nothing for an unreferenced item', () => {
		expect(computeReferences('items', 5, sources())).toEqual([]);
	});
});

describe('computeReferences — skills', () => {
	it('lists enemy skill-pool membership and challenge rewards', () => {
		expect(computeReferences('skills', 0, sources())).toEqual([
			{ kind: 'enemySkill', names: ['Cave Bat'] },
			{ kind: 'challengeReward', names: ['Bat Hunter'] }
		]);
	});

	it('lists skill-pool membership, reward, and challenge-target references together', () => {
		expect(computeReferences('skills', 1, sources())).toEqual([
			{ kind: 'enemySkill', names: ['Cave Bat'] },
			{ kind: 'challengeReward', names: ['Skill Spammer'] },
			{ kind: 'challengeTarget', names: ['Skill Spammer'] }
		]);
	});

	it('reports only skill-pool membership for a skill that is no reward or target', () => {
		expect(computeReferences('skills', 2, sources())).toEqual([{ kind: 'enemySkill', names: ['Catacomb Lich'] }]);
	});
});

describe('computeReferences — unknown entity', () => {
	it('returns no references for an unrecognized entity key', () => {
		expect(computeReferences('widgets', 0, sources())).toEqual([]);
	});
});

describe('formatReferenceBody', () => {
	it('builds a single-clause body and the resolvable-by-id reassurance', () => {
		const groups: ReferenceGroup[] = [{ kind: 'challengeReward', names: ['First Steps'] }];
		expect(formatReferenceBody('items', 'Iron Helm', groups)).toBe(
			'Iron Helm is the reward for 1 challenge (First Steps). Retiring takes it out of circulation for new content, though existing references still resolve by id.'
		);
	});

	it('joins two clauses with "and"', () => {
		const groups: ReferenceGroup[] = [
			{ kind: 'bossOf', names: ['Frost Cavern'] },
			{ kind: 'spawnsIn', names: ['Frost Cavern'] }
		];
		expect(formatReferenceBody('enemies', 'Catacomb Lich', groups)).toContain(
			'Catacomb Lich is the authored boss of Frost Cavern and in the spawn table of 1 zone (Frost Cavern).'
		);
	});

	it('joins three or more clauses with an Oxford comma', () => {
		const groups: ReferenceGroup[] = [
			{ kind: 'bossOf', names: ['Ember Wastes'] },
			{ kind: 'spawnsIn', names: ['Verdant Hollow', 'Frost Cavern'] },
			{ kind: 'challengeTarget', names: ['Bat Hunter'] }
		];
		const body = formatReferenceBody('enemies', 'Cave Bat', groups);
		expect(body).toContain(
			'the authored boss of Ember Wastes, in the spawn table of 2 zones (Verdant Hollow, Frost Cavern), and the target of 1 challenge (Bat Hunter)'
		);
	});

	it('appends the spawn-unsatisfiable note only for enemy challenge targets', () => {
		const groups: ReferenceGroup[] = [{ kind: 'challengeTarget', names: ['Bat Hunter'] }];
		expect(formatReferenceBody('enemies', 'Cave Bat', groups)).toContain(
			'which may become unsatisfiable once it no longer spawns randomly'
		);
		expect(formatReferenceBody('zones', 'Frost Cavern', groups)).not.toContain('unsatisfiable');
	});

	it('spells out the lockout consequence for an unlock-gate reference', () => {
		const groups: ReferenceGroup[] = [{ kind: 'unlockGate', names: ['Frost Cavern', 'Ember Wastes'], strong: true }];
		expect(formatReferenceBody('challenges', 'First Steps', groups)).toContain(
			'the unlock gate for Frost Cavern, Ember Wastes — new players will be unable to enter them'
		);
	});

	it('closes a strong reference by pointing at the fix instead of the generic reassurance', () => {
		const strong: ReferenceGroup[] = [{ kind: 'unlockGate', names: ['Frost Cavern'], strong: true }];
		const normal: ReferenceGroup[] = [{ kind: 'challengeReward', names: ['First Steps'] }];
		expect(formatReferenceBody('challenges', 'First Steps', strong)).toContain(
			're-point the affected records first if that consequence is unintended'
		);
		expect(formatReferenceBody('challenges', 'First Steps', strong)).not.toContain(
			'out of circulation for new content'
		);
		expect(formatReferenceBody('items', 'Iron Helm', normal)).toContain('out of circulation for new content');
	});

	it('caps long name lists with a "+N more" tail', () => {
		const names = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'];
		const body = formatReferenceBody('zones', 'Verdant Hollow', [{ kind: 'spawnedBy', names }]);
		expect(body).toContain('the spawn location for 8 enemies (A, B, C, D, E, F +2 more)');
	});
});
