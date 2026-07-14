import { describe, it, expect } from 'vitest';
import {
	EAttribute,
	EChallengeType,
	EEntityType,
	EItemCategory,
	EModifierType,
	ERarity,
	ESkillAcquisition,
	type IChallenge,
	type IClass,
	type IEnemy,
	type IItem,
	type IProficiency,
	type ISkill,
	type ISkillRecipe,
	type IZone
} from '$lib/api';
import {
	computeReferences,
	formatReferenceBody,
	type ReferenceGroup,
	type ReferenceSources
} from '$routes/admin/workbench/references';

const enemy = (id: number, name: string, opts: Partial<IEnemy> = {}): IEnemy => ({
	id,
	name,
	designerNotes: '',
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
	designerNotes: '',
	order: 0,
	levelMin: 1,
	levelMax: 10,
	bossLevel: 1,
	isHome: false,
	...opts
});

const challenge = (id: number, name: string, opts: Partial<IChallenge> = {}): IChallenge => ({
	id,
	name,
	description: '',
	designerNotes: '',
	challengeTypeId: EChallengeType.EnemiesKilled,
	entityType: EEntityType.None,
	progressGoal: 10,
	...opts
});

const item = (id: number, name: string, opts: Partial<IItem> = {}): IItem => ({
	id,
	name,
	description: '',
	itemCategoryId: EItemCategory.Helm,
	rarityId: ERarity.Common,
	iconPath: '',
	attributes: [],
	modSlots: [],
	tags: [],
	requiredProficiencyLevel: 1,
	designerNotes: '',
	...opts
});

const wclass = (id: number, name: string, opts: Partial<IClass> = {}): IClass => ({
	id,
	name,
	description: '',
	word: '',
	passiveAttributeId: EAttribute.Strength,
	passiveAmount: 0,
	passiveScalingAmount: 0,
	passiveModifierType: EModifierType.Additive,
	starterSkillIds: [],
	starterEquipment: [],
	attributeDistributions: [],
	designerNotes: '',
	...opts
});

const recipe = (id: number, resultSkillId: number, opts: Partial<ISkillRecipe> = {}): ISkillRecipe => ({
	id,
	resultSkillId,
	designerNotes: '',
	inputSkillIds: [],
	conditions: [],
	...opts
});

const proficiency = (id: number, name: string, opts: Partial<IProficiency> = {}): IProficiency => ({
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
	xpGrowth: 1.4,
	designerNotes: '',
	levelModifiers: [],
	levelRewards: [],
	prerequisiteIds: [],
	...opts
});

/** Places each record at its own id as the array index, honouring the zero-based-id/index invariant. */
const bySlot = <T extends { id: number }>(...items: T[]): T[] => {
	const arr: T[] = [];
	for (const item of items) {
		arr[item.id] = item;
	}
	return arr;
};

const skill = (id: number, name: string): ISkill => ({
	id,
	name,
	baseDamage: 10,
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	damagePortions: [],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	acquisition: ESkillAcquisition.Player,
	designerNotes: ''
});

// Id-aligned reference sets shared across the computation cases. Only enemies/zones/challenges carry
// cross-referencing fixture data by default — the item/class/recipe/proficiency describes below layer
// their own scenario-specific entries on top via spread so they can't perturb the base assertions.
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
			rewardItemModId: 1
		}),
		challenge(2, 'Zone Clearer', { entityType: EEntityType.Zone, targetEntityId: 1 }),
		challenge(3, 'Skill Spammer', { entityType: EEntityType.Skill, targetEntityId: 1 })
	],
	items: [],
	classes: [],
	skillRecipes: [],
	proficiencies: [],
	skills: []
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
	it('lists enemy skill-pool membership', () => {
		expect(computeReferences('skills', 0, sources())).toEqual([{ kind: 'enemySkill', names: ['Cave Bat'] }]);
	});

	it('lists skill-pool membership and challenge-target references together', () => {
		expect(computeReferences('skills', 1, sources())).toEqual([
			{ kind: 'enemySkill', names: ['Cave Bat'] },
			{ kind: 'challengeTarget', names: ['Skill Spammer'] }
		]);
	});

	it('reports only skill-pool membership for a skill that is no target', () => {
		expect(computeReferences('skills', 2, sources())).toEqual([{ kind: 'enemySkill', names: ['Catacomb Lich'] }]);
	});
});

describe('computeReferences — skills, extended (item grant, starter kit, recipes, milestones)', () => {
	it('lists the item that grants the skill', () => {
		const src = { ...sources(), items: [item(0, 'Runed Blade', { grantedSkillId: 5 })] };
		expect(computeReferences('skills', 5, src)).toEqual([{ kind: 'grantedSkillOf', names: ['Runed Blade'] }]);
	});

	it('lists the class the skill is a starter skill of', () => {
		const src = { ...sources(), classes: [wclass(0, 'Warden', { starterSkillIds: [5] })] };
		expect(computeReferences('skills', 5, src)).toEqual([{ kind: 'starterSkillOf', names: ['Warden'] }]);
	});

	it('lists the synthesis recipe the skill is the result of, named by its own result skill', () => {
		const src = {
			...sources(),
			skillRecipes: [recipe(0, 5)],
			skills: bySlot(skill(0, 'Fire Bolt'), skill(1, 'Ice Shard'), skill(2, 'Slam'), skill(5, 'Meteor'))
		};
		expect(computeReferences('skills', 5, src)).toEqual([{ kind: 'recipeResult', names: ['Meteor'] }]);
	});

	it('lists the synthesis recipe the skill is an input of', () => {
		const src = {
			...sources(),
			skillRecipes: [recipe(0, 6, { inputSkillIds: [5] })],
			skills: bySlot(skill(0, 'Fire Bolt'), skill(1, 'Ice Shard'), skill(2, 'Slam'), skill(6, 'Meteor'))
		};
		expect(computeReferences('skills', 5, src)).toEqual([{ kind: 'recipeInput', names: ['Meteor'] }]);
	});

	it('lists the proficiency that grants the skill as a milestone reward', () => {
		const src = {
			...sources(),
			proficiencies: [proficiency(0, 'Blades', { levelRewards: [{ level: 3, rewardSkillId: 5 }] })]
		};
		expect(computeReferences('skills', 5, src)).toEqual([{ kind: 'milestoneReward', names: ['Blades'] }]);
	});

	it('combines every skill-referencing kind together', () => {
		const src: ReferenceSources = {
			...sources(),
			items: [item(0, 'Runed Blade', { grantedSkillId: 5 })],
			classes: [wclass(0, 'Warden', { starterSkillIds: [5] })],
			skillRecipes: [recipe(0, 6, { inputSkillIds: [5] })],
			skills: bySlot(skill(6, 'Meteor')),
			proficiencies: [proficiency(0, 'Blades', { levelRewards: [{ level: 3, rewardSkillId: 5 }] })]
		};
		expect(computeReferences('skills', 5, src)).toEqual([
			{ kind: 'grantedSkillOf', names: ['Runed Blade'] },
			{ kind: 'starterSkillOf', names: ['Warden'] },
			{ kind: 'recipeInput', names: ['Meteor'] },
			{ kind: 'milestoneReward', names: ['Blades'] }
		]);
	});
});

describe('computeReferences — items, extended (starter equipment)', () => {
	it('lists the class that equips the item at character creation', () => {
		const src = {
			...sources(),
			classes: [wclass(0, 'Warden', { starterEquipment: [{ itemId: 7, equipmentSlot: 0 }] })]
		};
		expect(computeReferences('items', 7, src)).toEqual([{ kind: 'starterEquipmentOf', names: ['Warden'] }]);
	});

	it('combines the challenge-reward and starter-equipment kinds together', () => {
		const src: ReferenceSources = {
			...sources(),
			classes: [wclass(0, 'Warden', { starterEquipment: [{ itemId: 0, equipmentSlot: 0 }] })]
		};
		// Item 0 is already the reward for "First Steps" in the base fixture.
		expect(computeReferences('items', 0, src)).toEqual([
			{ kind: 'challengeReward', names: ['First Steps'] },
			{ kind: 'starterEquipmentOf', names: ['Warden'] }
		]);
	});
});

describe('computeReferences — proficiencies', () => {
	it('lists the item that gates on the proficiency', () => {
		const src = { ...sources(), items: [item(0, 'Iron Helm', { requiredProficiencyId: 5 })] };
		expect(computeReferences('proficiencies', 5, src)).toEqual([{ kind: 'gearGate', names: ['Iron Helm'] }]);
	});

	it('lists the synthesis recipe conditioned on the proficiency', () => {
		const src = {
			...sources(),
			skillRecipes: [recipe(0, 9, { conditions: [{ proficiencyId: 5, minLevel: 3 }] })],
			skills: bySlot(skill(9, 'Meteor'))
		};
		expect(computeReferences('proficiencies', 5, src)).toEqual([{ kind: 'recipeCondition', names: ['Meteor'] }]);
	});

	it('lists the other tier that gates on this one as a cross-path prerequisite', () => {
		const src = { ...sources(), proficiencies: [proficiency(6, 'Advanced Blades', { prerequisiteIds: [5] })] };
		expect(computeReferences('proficiencies', 5, src)).toEqual([
			{ kind: 'prerequisiteOf', names: ['Advanced Blades'] }
		]);
	});

	it('returns nothing for an unreferenced proficiency', () => {
		expect(computeReferences('proficiencies', 5, sources())).toEqual([]);
	});
});

describe('computeReferences — paths', () => {
	it('lists a live gateway that would soft-lock, naming the gating tier by its own tier name', () => {
		const src = {
			...sources(),
			proficiencies: [
				proficiency(0, 'Blades', { pathId: 5 }),
				proficiency(1, 'Advanced Blades', { pathId: 5 }),
				proficiency(6, 'Runeforging', { pathId: 9, prerequisiteIds: [0] })
			]
		};
		expect(computeReferences('paths', 5, src)).toEqual([
			{ kind: 'prerequisiteOf', names: ['Runeforging'], strong: true }
		]);
	});

	it('ignores a prerequisite edge between two tiers of the same path being retired', () => {
		const src = {
			...sources(),
			proficiencies: [
				proficiency(0, 'Blades', { pathId: 5 }),
				proficiency(1, 'Advanced Blades', { pathId: 5, prerequisiteIds: [0] })
			]
		};
		expect(computeReferences('paths', 5, src)).toEqual([]);
	});

	it('returns nothing for a path whose tiers gate nothing', () => {
		const src = { ...sources(), proficiencies: [proficiency(0, 'Blades', { pathId: 5 })] };
		expect(computeReferences('paths', 5, src)).toEqual([]);
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

	it('phrases the item/class/recipe/proficiency reference kinds added for #1517', () => {
		expect(formatReferenceBody('skills', 'Meteor', [{ kind: 'grantedSkillOf', names: ['Runed Blade'] }])).toContain(
			'the skill granted by 1 item (Runed Blade)'
		);
		expect(formatReferenceBody('skills', 'Meteor', [{ kind: 'starterSkillOf', names: ['Warden'] }])).toContain(
			'a starter skill of 1 class (Warden)'
		);
		expect(formatReferenceBody('items', 'Iron Helm', [{ kind: 'starterEquipmentOf', names: ['Warden'] }])).toContain(
			'starter equipment for 1 class (Warden)'
		);
		expect(formatReferenceBody('skills', 'Meteor', [{ kind: 'recipeResult', names: ['Meteor'] }])).toContain(
			'the result of 1 synthesis recipe (Meteor)'
		);
		expect(formatReferenceBody('skills', 'Meteor', [{ kind: 'recipeInput', names: ['Meteor'] }])).toContain(
			'an input of 1 synthesis recipe (Meteor)'
		);
		expect(formatReferenceBody('skills', 'Meteor', [{ kind: 'milestoneReward', names: ['Blades'] }])).toContain(
			'a milestone reward of 1 proficiency (Blades)'
		);
		expect(formatReferenceBody('proficiencies', 'Blades', [{ kind: 'gearGate', names: ['Iron Helm'] }])).toContain(
			'the required proficiency for 1 item (Iron Helm)'
		);
		expect(formatReferenceBody('proficiencies', 'Blades', [{ kind: 'recipeCondition', names: ['Meteor'] }])).toContain(
			'a condition of 1 synthesis recipe (Meteor)'
		);
		expect(
			formatReferenceBody('proficiencies', 'Blades', [{ kind: 'prerequisiteOf', names: ['Advanced Blades'] }])
		).toContain('a prerequisite for 1 proficiency (Advanced Blades)');
	});

	it('phrases a path soft-lock reference (#1863) as owning the gating tier, not being the prerequisite itself, and closes with the strong-reference fix-first wording', () => {
		const groups: ReferenceGroup[] = [{ kind: 'prerequisiteOf', names: ['Runeforging'], strong: true }];
		const body = formatReferenceBody('paths', 'Blades', groups);
		expect(body).toContain('home to a tier that gates 1 proficiency (Runeforging)');
		expect(body).toContain('re-point the affected records first if that consequence is unintended');
	});

	it('keeps the proficiency-retire prerequisiteOf phrasing unchanged (the proficiency itself is the prerequisite)', () => {
		expect(
			formatReferenceBody('proficiencies', 'Blades', [{ kind: 'prerequisiteOf', names: ['Advanced Blades'] }])
		).toContain('a prerequisite for 1 proficiency (Advanced Blades)');
	});
});
