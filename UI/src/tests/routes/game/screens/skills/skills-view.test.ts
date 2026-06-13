import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EAttribute, type IChallenge, type IEnemy, type ISkill, type IZone } from '$lib/api';

// Engine + stores are mocked so importing the view-model doesn't drag in the real
// game engine. `playerManager.unlockedSkills` is a plain writable array (the view
// reassigns it on persist) and `selectedSkills` derives the equipped order from it,
// mirroring the real PlayerManager. `vi.hoisted` keeps these ready before the
// hoisted vi.mock factories run.
const { mockPlayerManager, mockInventoryManager, sendSocketCommand, toastError, staticData } = vi.hoisted(() => {
	const playerManager = {
		unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
		currentZone: 0,
		attributes: [] as { attributeId: number; amount: number }[],
		get selectedSkills(): number[] {
			return playerManager.unlockedSkills
				.filter((s) => s.selected)
				.sort((a, b) => (a.order ?? 0) - (b.order ?? 0))
				.map((s) => s.skillId);
		}
	};
	return {
		mockPlayerManager: playerManager,
		mockInventoryManager: { equipmentStats: [] as { attributeId: number; amount: number }[] },
		sendSocketCommand: vi.fn(),
		toastError: vi.fn(),
		staticData: {
			skills: [] as ISkill[],
			challenges: [] as IChallenge[],
			zones: undefined as IZone[] | undefined,
			enemies: undefined as IEnemy[] | undefined,
			attributes: undefined as unknown
		}
	};
});

vi.mock('$lib/engine', () => ({ playerManager: mockPlayerManager, inventoryManager: mockInventoryManager }));
vi.mock('$stores', () => ({ staticData, toastError }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, apiSocket: { sendSocketCommand } };
});
// Pin the generated loadout cap so the full-loadout/swap scenarios below stay coherent (the
// fixtures equip three of four unlocked skills); the cap's behavior is what's under test, not its
// value. Partial mock — the other constants must stay real for the transitively-imported engine.
vi.mock('$lib/api/types/game-constants', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, MAX_SELECTED_SKILLS: 3 };
});

import {
	SkillsView,
	damagePerSecond,
	sortMetrics,
	zoneSpawnLevel,
	enemyDefense,
	type SkillMetrics
} from '$routes/game/screens/skills/skills-view.svelte';

/* A skill with sensible defaults so each test only states what matters. */
const skill = (over: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${over.id}`,
	baseDamage: 0,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	...over
});

const metric = (over: Partial<SkillMetrics> & { skill: ISkill }): SkillMetrics => ({
	unlocked: true,
	rawDamage: 0,
	cooldown: 1,
	contributions: [],
	...over
});

/* The sample catalogue: ids 0–2 are equipped starters, 3 is unlocked-but-unequipped,
   4 is locked, 5 is retired-and-unowned (excluded from the catalogue). */
const SKILLS: ISkill[] = [
	skill({
		id: 0,
		name: 'Alpha',
		baseDamage: 12,
		cooldownMs: 1000,
		damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }]
	}),
	skill({
		id: 1,
		name: 'Bravo',
		baseDamage: 40,
		cooldownMs: 2000,
		damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 2 }]
	}),
	skill({ id: 2, name: 'Charlie', baseDamage: 5, cooldownMs: 500 }),
	skill({
		id: 3,
		name: 'Delta',
		baseDamage: 50,
		cooldownMs: 1000,
		damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 1 }]
	}),
	skill({
		id: 4,
		name: 'Echo',
		baseDamage: 100,
		cooldownMs: 4000,
		damageMultipliers: [{ attributeId: EAttribute.Intellect, multiplier: 1 }]
	}),
	skill({ id: 5, name: 'Foxtrot', baseDamage: 8, cooldownMs: 800, retiredAt: '2026-01-01T00:00:00Z' })
];

// Only `name` + `rewardSkillId` matter here; the enum-typed fields are cast.
const CHALLENGES = [
	{ id: 0, name: 'Slay Ten', description: '', challengeTypeId: 0, entityType: 0, progressGoal: 10, rewardSkillId: 3 },
	{
		id: 1,
		name: 'Clear the Vale',
		description: '',
		challengeTypeId: 0,
		entityType: 0,
		progressGoal: 1,
		rewardSkillId: 4
	}
] as unknown as IChallenge[];

/* Compare-vs fixtures. Enemy Defense resolves through the real BattleAttributes as
   `2 (base) + 1·Endurance + 0.5·Agility`, so each enemy's distribution is chosen for a
   clean expected value: with only per-level Endurance, Defense = 2 + amountPerLevel·level. */
const enemy = (over: Partial<IEnemy> & { id: number }): IEnemy => ({
	name: `Enemy ${over.id}`,
	isBoss: false,
	attributeDistribution: [],
	skillPool: [],
	spawns: [],
	...over
});

const enemyDist = (endurancePerLevel: number) => [
	{ attributeId: EAttribute.Endurance, baseAmount: 0, amountPerLevel: endurancePerLevel }
];

// Zone 0: idle range [2, 8] (midpoint level 5), dedicated boss enemy 2 fought at level 10.
const ZONES: IZone[] = [
	{
		id: 0,
		name: 'Vale',
		description: '',
		order: 0,
		levelMin: 2,
		levelMax: 8,
		bossEnemyId: 2,
		bossLevel: 10
	}
];

const ENEMIES: IEnemy[] = [
	enemy({ id: 0, name: 'Imp', attributeDistribution: enemyDist(2), spawns: [{ zoneId: 0, weight: 1 }] }),
	enemy({ id: 1, name: 'Wolf', attributeDistribution: enemyDist(5), spawns: [{ zoneId: 1, weight: 1 }] }),
	enemy({ id: 2, name: 'Ogre King', isBoss: true, attributeDistribution: enemyDist(3) }),
	enemy({
		id: 3,
		name: 'Wraith',
		attributeDistribution: enemyDist(9),
		spawns: [{ zoneId: 0, weight: 1 }],
		retiredAt: '2026-01-01T00:00:00Z'
	})
];

const lastPayload = (): number[] => sendSocketCommand.mock.calls.at(-1)?.[1] as number[];

let view: SkillsView;

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({});
	toastError.mockReset();
	staticData.skills = SKILLS;
	staticData.challenges = CHALLENGES;
	staticData.zones = ZONES;
	staticData.enemies = ENEMIES;
	mockPlayerManager.currentZone = 0;
	// ids 0–3 unlocked; 0–2 equipped in order; 4 and 5 not owned.
	mockPlayerManager.unlockedSkills = [
		{ skillId: 0, selected: true, order: 0 },
		{ skillId: 1, selected: true, order: 1 },
		{ skillId: 2, selected: true, order: 2 },
		{ skillId: 3, selected: false, order: 0 }
	];
	mockPlayerManager.attributes = [];
	mockInventoryManager.equipmentStats = [];
	view = new SkillsView();
});

/* The raw-damage / defense-subtraction / cooldown-multiplier formulas are the shared battle
   formulas (`$lib/battle/battle-formulas`), unit-tested in `tests/lib/battle/battle-formulas.test.ts`;
   here only the page-local helpers and the view's consumption of the shared ones are covered. */
describe('pure helpers', () => {
	it('returns zero dps for a non-positive cooldown', () => {
		expect(damagePerSecond(50, 2)).toBe(25);
		expect(damagePerSecond(50, 0)).toBe(0);
	});

	it('sorts by name, ascending cooldown, and defense-aware dps/damage', () => {
		const a = metric({ skill: skill({ id: 0, name: 'Zeta' }), rawDamage: 100, cooldown: 2 });
		const b = metric({ skill: skill({ id: 1, name: 'Alpha' }), rawDamage: 30, cooldown: 1 });

		expect([a, b].sort(sortMetrics('name', 0)).map((m) => m.skill.name)).toEqual(['Alpha', 'Zeta']);
		expect([a, b].sort(sortMetrics('cd', 0)).map((m) => m.skill.id)).toEqual([1, 0]);
		// dps with no defense: a=50, b=30 → a first.
		expect([b, a].sort(sortMetrics('dps', 0)).map((m) => m.skill.id)).toEqual([0, 1]);
		// dps with defense 80: a=(100-80)/2=10, b=0 → a first; damage a=20, b=0.
		expect([b, a].sort(sortMetrics('dmg', 80)).map((m) => m.skill.id)).toEqual([0, 1]);
	});

	it('takes the midpoint of a zone range as the representative spawn level', () => {
		expect(zoneSpawnLevel(ZONES[0])).toBe(5); // (2 + 8) / 2
	});

	it('resolves an enemy Defense through the derived attribute composition', () => {
		// Defense = 2 (base) + 1·Endurance + 0.5·Agility. Imp has 2·level Endurance only.
		expect(enemyDefense(ENEMIES[0], 5)).toBe(12); // 2 + 2*5
		expect(enemyDefense(ENEMIES[2], 10)).toBe(32); // boss: 2 + 3*10
	});
});

describe('SkillsView — initial state', () => {
	it('seeds the working loadout from the equipped order', () => {
		expect(view.equipped).toEqual([0, 1, 2]);
		expect(view.selectedId).toBe(0);
		expect(view.cap).toBe(3);
	});

	it('excludes retired-and-unowned skills but keeps locked ones in the catalogue', () => {
		const ids = view.catalogue.map((s) => s.id);
		expect(ids).toContain(4); // locked, not retired → aspirational
		expect(ids).not.toContain(5); // retired and unowned
	});

	it('exposes the skill catalogue defense ceiling for the slider', () => {
		expect(view.maxDamage).toBe(100); // skill 4 base damage
	});

	it('resolves metrics through the shared battle formulas and the derived attribute composition', () => {
		// Strength feeds Alpha's 1× multiplier directly; Agility derives CooldownRecovery (0.4/pt).
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.Strength, amount: 20 },
			{ attributeId: EAttribute.Agility, amount: 10 }
		];
		const fresh = new SkillsView();
		expect(fresh.rawDamage(0)).toBe(32); // 12 base + 20·1
		expect(fresh.cooldown(0)).toBeCloseTo(1 / 1.04, 10); // 1s / (1 + 4 CDR / 100)
		expect(fresh.metric(0)?.contributions).toEqual([{ attributeId: EAttribute.Strength, multiplier: 1, value: 20 }]);
	});
});

describe('SkillsView — rail filtering & sorting', () => {
	it('hides locked skills until showLocked is on', () => {
		expect(view.railList.map((m) => m.skill.id)).not.toContain(4);
		view.toggleShowLocked();
		expect(view.railList.map((m) => m.skill.id)).toContain(4);
	});

	it('filters by attribute', () => {
		view.toggleShowLocked();
		view.toggleAttributeFilter(EAttribute.Strength);
		expect(view.railList.map((m) => m.skill.id).sort()).toEqual([0, 3]);
	});

	it('filters by search term', () => {
		view.search = 'alp';
		expect(view.railList.map((m) => m.skill.id)).toEqual([0]);
	});

	it('ranks by effective dps and flips order once defense is applied', () => {
		view.setSort('dps');
		// no defense: dps = base/cd → Delta(50) > Bravo(20) > Alpha(12) > Charlie(10)
		expect(view.railList.map((m) => m.skill.id)).toEqual([3, 1, 0, 2]);
		// defense 15 blocks Alpha/Charlie entirely; Bravo's higher base keeps it ahead.
		view.setDefense(15);
		const ranked = view.railList.map((m) => m.skill.id);
		expect(ranked.slice(0, 2)).toEqual([3, 1]);
	});

	it('groups equipped rows by slot and lists the rest as available', () => {
		expect(view.equippedRail.map((m) => m.skill.id)).toEqual([0, 1, 2]);
		expect(view.availableRail.map((m) => m.skill.id)).toEqual([3]);
	});
});

describe('SkillsView — loadout edits persist the ordered loadout', () => {
	it('unequips a skill and sends the reduced loadout', () => {
		view.toggle(0);
		expect(view.equipped).toEqual([1, 2]);
		expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2]);
	});

	it('equips into a free slot and sends the extended loadout', () => {
		view.toggle(0); // free a slot first (cap is 3)
		view.toggle(3);
		expect(view.equipped).toEqual([1, 2, 3]);
		expect(lastPayload()).toEqual([1, 2, 3]);
	});

	it('starts a swap (no command) when equipping into a full loadout', () => {
		view.toggle(3);
		expect(view.pendingSwap).toBe(3);
		expect(view.equipped).toEqual([0, 1, 2]);
		expect(sendSocketCommand).not.toHaveBeenCalled();
	});

	it('resolves a swap by replacing the chosen slot', () => {
		view.toggle(3); // full → pending swap
		view.swapInto(1);
		expect(view.equipped).toEqual([0, 3, 2]);
		expect(view.selectedId).toBe(3);
		expect(view.pendingSwap).toBeNull();
		expect(lastPayload()).toEqual([0, 3, 2]);
	});

	it('reorders the loadout', () => {
		view.reorder(0, 2);
		expect(view.equipped).toEqual([1, 2, 0]);
		expect(lastPayload()).toEqual([1, 2, 0]);
	});

	it('ignores toggling a locked skill', () => {
		view.toggle(4);
		expect(view.equipped).toEqual([0, 1, 2]);
		expect(view.pendingSwap).toBeNull();
		expect(sendSocketCommand).not.toHaveBeenCalled();
	});

	it('mirrors the new loadout onto the player manager', () => {
		view.reorder(0, 2); // → [1, 2, 0]
		expect(mockPlayerManager.selectedSkills).toEqual([1, 2, 0]);
		const delta = mockPlayerManager.unlockedSkills.find((s) => s.skillId === 3);
		expect(delta?.selected).toBe(false);
	});
});

describe('SkillsView — persistence failure', () => {
	it('reverts the loadout and surfaces a toast when the command errors', async () => {
		sendSocketCommand.mockResolvedValueOnce({ error: 'nope' });
		view.toggle(0); // attempt to unequip
		// Optimistic state applied synchronously…
		expect(view.equipped).toEqual([1, 2]);
		await vi.waitFor(() => expect(toastError).toHaveBeenCalled());
		// …then reverted once the failed command resolves.
		expect(view.equipped).toEqual([0, 1, 2]);
		expect(mockPlayerManager.selectedSkills).toEqual([0, 1, 2]);
	});
});

describe('SkillsView — compare-vs defense', () => {
	it('clamps the defense to the slider range', () => {
		view.setDefense(-5);
		expect(view.defense).toBe(0);
		view.setDefense(9999);
		expect(view.defense).toBe(view.maxDamage);
	});

	it('reports combined effective dps/burst over the equipped loadout', () => {
		// base damages of [0,1,2] = 12 + 40 + 5 = 57 at zero defense.
		expect(Math.round(view.combinedEffectiveBurst)).toBe(57);
		expect(view.combinedEffectiveDps).toBeGreaterThan(0);
	});
});

describe('SkillsView — compare-vs enemy presets', () => {
	it('sources pills from the current zone: in-zone spawns (midpoint level) plus the boss', () => {
		const presets = view.comparePresets;
		// Imp spawns in zone 0; Wolf spawns elsewhere; Wraith is retired; Ogre King is the boss.
		expect(presets.map((p) => p.key)).toEqual(['spawn-0', 'boss-2']);
		expect(presets[0]).toMatchObject({ name: 'Imp', isBoss: false, defense: 12 }); // 2 + 2*5
		expect(presets[1]).toMatchObject({ name: 'Ogre King', isBoss: true, defense: 32 }); // 2 + 3*10
	});

	it('is empty when the current zone has no reference data yet', () => {
		staticData.zones = undefined;
		const fresh = new SkillsView();
		expect(fresh.comparePresets).toEqual([]);
	});

	it('snaps the slider to a preset and marks its pill selected', () => {
		const [spawn, boss] = view.comparePresets;
		view.selectPreset(spawn);
		expect(view.defense).toBe(12);
		expect(view.selectedPresetKey).toBe('spawn-0');

		view.selectPreset(boss);
		expect(view.defense).toBe(32);
		expect(view.selectedPresetKey).toBe('boss-2');
	});

	it('clamps a preset defense above the slider ceiling but keeps the pill selected', () => {
		view.selectPreset({ key: 'spawn-9', name: 'Colossus', isBoss: false, defense: 9999 });
		expect(view.defense).toBe(view.maxDamage);
		expect(view.selectedPresetKey).toBe('spawn-9');
	});

	it('deselects the active pill when the slider is dragged manually', () => {
		view.selectPreset(view.comparePresets[0]);
		expect(view.selectedPresetKey).toBe('spawn-0');
		view.setDefense(7);
		expect(view.defense).toBe(7);
		expect(view.selectedPresetKey).toBeNull();
	});

	it('resorts railList by effective dps when a preset is selected', () => {
		view.setSort('dps');
		// At 0 defense: Delta(50/1=50) > Bravo(40/2=20) > Alpha(12/1=12) > Charlie(5/0.5=10).
		expect(view.railList.map((m) => m.skill.id)).toEqual([3, 1, 0, 2]);

		// Imp (defense=12): Delta=(50-12)/1=38, Bravo=(40-12)/2=14, Alpha=Charlie=0.
		const [spawn] = view.comparePresets;
		view.selectPreset(spawn);
		expect(view.defense).toBe(spawn.defense);
		expect(view.railList.map((m) => m.skill.id).slice(0, 2)).toEqual([3, 1]);
	});
});
