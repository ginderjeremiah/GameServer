import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
	ERarity,
	EAttribute,
	ESkillAcquisition,
	type IChallenge,
	type IEnemy,
	type ISkill,
	type IZone
} from '$lib/api';

// Engine + stores are mocked so importing the view-model doesn't drag in the real
// game engine. `playerManager.unlockedSkills` is a plain writable array and
// `setSelectedSkills`/`selectedSkills` mirror the real PlayerManager (the view now
// routes loadout changes through the manager). `vi.hoisted` keeps these ready before
// the hoisted vi.mock factories run.
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
		},
		setSelectedSkills(orderedIds: number[]) {
			for (const unlockedSkill of playerManager.unlockedSkills) {
				const order = orderedIds.indexOf(unlockedSkill.skillId);
				unlockedSkill.selected = order >= 0;
				unlockedSkill.order = order >= 0 ? order : undefined;
			}
		}
	};
	return {
		mockPlayerManager: playerManager,
		mockInventoryManager: {
			equipmentStats: [] as { attributeId: number; amount: number }[],
			equippedSlots: [] as ({ grantedSkillId?: number; name: string } | undefined)[]
		},
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
	sortMetrics,
	zoneSpawnLevel,
	type SkillMetrics
} from '$routes/game/screens/skills/skills-view.svelte';
import { damagePerSecond } from '$lib/common';
import { expectedCritMultiplier } from '$lib/battle';

/* A skill with sensible defaults so each test only states what matters. */
const skill = (over: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${over.id}`,
	baseDamage: 0,
	damageMultipliers: [],
	effects: [],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	acquisition: ESkillAcquisition.Player,
	...over
});

const metric = (over: Partial<SkillMetrics> & { skill: ISkill }): SkillMetrics => ({
	rawDamage: 0,
	cooldown: 1,
	contributions: [],
	...over
});

/* The sample catalogue: ids 0–2 are equipped starters, 3 is unlocked-but-unequipped,
   4 is not owned (excluded from the catalogue), 5 is retired-and-unowned (also excluded). */
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
	mockInventoryManager.equippedSlots = [];
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

	it('folds the crit multiplier into the effective dps/damage ranking', () => {
		// Two skills whose effective-DPS order flips once a global crit multiplier scales raw damage
		// before defense (a crit punches through Defense, mirroring the battle and the displayed numbers).
		const a = metric({ skill: skill({ id: 0 }), rawDamage: 30, cooldown: 3 });
		const b = metric({ skill: skill({ id: 1 }), rawDamage: 22, cooldown: 2 });
		// defense 10, no crit: A=(30−10)/3≈6.67 > B=(22−10)/2=6 → A first.
		expect([b, a].sort(sortMetrics('dps', 10)).map((m) => m.skill.id)).toEqual([0, 1]);
		// crit ×2: A=(60−10)/3≈16.67 < B=(44−10)/2=17 → B overtakes.
		expect([a, b].sort(sortMetrics('dps', 10, 2)).map((m) => m.skill.id)).toEqual([1, 0]);
		// dmg sort, defense 25: no crit both 5/0 → stable; ×2 lifts A=(60−25)=35 > B=(44−25)=19.
		expect([a, b].sort(sortMetrics('dmg', 25, 2)).map((m) => m.skill.id)).toEqual([0, 1]);
	});
});

describe('SkillsView — initial state', () => {
	it('seeds the working loadout from the equipped order', () => {
		expect(view.equipped).toEqual([0, 1, 2]);
		expect(view.selectedId).toBe(0);
		expect(view.cap).toBe(3);
	});

	it('lists only the player’s unlocked skills, excluding unowned and retired-unowned ones', () => {
		const ids = view.catalogue.map((s) => s.id);
		expect(ids).toEqual([0, 1, 2, 3]); // the unlocked set, in catalogue order
		expect(ids).not.toContain(4); // not owned
		expect(ids).not.toContain(5); // retired and unowned
	});

	it('keeps a retired skill the player still owns in the catalogue', () => {
		// Retirement keeps an owned record resolvable, so a retired-but-owned skill stays listed.
		mockPlayerManager.unlockedSkills = [...mockPlayerManager.unlockedSkills, { skillId: 5, selected: false, order: 0 }];
		const fresh = new SkillsView();
		expect(fresh.catalogue.map((s) => s.id)).toContain(5);
	});

	it('exposes unlocked ids as a set for O(1) membership', () => {
		expect(view.unlockedIds).toBeInstanceOf(Set);
		expect(view.unlockedIds.has(0)).toBe(true); // unlocked starter
		expect(view.unlockedIds.has(4)).toBe(false); // not owned
	});

	it('exposes the skill catalogue defense ceiling for the slider', () => {
		expect(view.maxDamage).toBe(50); // skill 3 (Delta) base damage — the largest unlocked
	});

	it('resolves metrics through the shared battle formulas and the derived attribute composition', () => {
		// Strength feeds Alpha's 1× multiplier directly; Agility derives CooldownRecovery (0.004/pt).
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.Strength, amount: 20 },
			{ attributeId: EAttribute.Agility, amount: 10 }
		];
		const fresh = new SkillsView();
		expect(fresh.rawDamage(0)).toBe(32); // 12 base + 20·1
		// CooldownRecovery = base 1 + 0.004·10 = 1.04, read directly as the cooldown multiplier.
		expect(fresh.cooldown(0)).toBeCloseTo(1 / 1.04, 10);
		expect(fresh.metric(0)?.contributions).toEqual([{ attributeId: EAttribute.Strength, multiplier: 1, value: 20 }]);
	});
});

describe('SkillsView — innate (item-granted) skills', () => {
	it('is empty when no equipped item grants a skill', () => {
		expect(view.innateSkills).toEqual([]);
	});

	it('lists granted skills in equipped-slot order with their source item, skipping non-granting items', () => {
		mockInventoryManager.equippedSlots = [
			{ name: 'Helm of Echoes', grantedSkillId: 3 }, // slot 0
			undefined,
			{ name: 'Plain Greaves' }, // slot 2, no grant
			{ name: 'Staff of Embers', grantedSkillId: 4 } // slot 3
		];
		const fresh = new SkillsView();

		expect(fresh.innateSkills.map((g) => [g.skill.id, g.sourceItemName, g.duplicate])).toEqual([
			[3, 'Helm of Echoes', false],
			[4, 'Staff of Embers', false]
		]);
	});

	it('flags a grant that duplicates a selected skill as already in the loadout', () => {
		// Skill 0 is in the equipped loadout, so an item granting it is a duplicate (fielded once).
		mockInventoryManager.equippedSlots = [{ name: 'Echo Blade', grantedSkillId: 0 }];
		const fresh = new SkillsView();

		const innate = fresh.innateSkills;
		expect(innate).toHaveLength(1);
		expect(innate[0]).toMatchObject({ duplicate: true, sourceItemName: 'Echo Blade' });
	});

	it('flags a second item granting the same skill as a duplicate', () => {
		mockInventoryManager.equippedSlots = [
			{ name: 'First Axe', grantedSkillId: 3 },
			{ name: 'Second Axe', grantedSkillId: 3 }
		];
		const fresh = new SkillsView();

		expect(fresh.innateSkills.map((g) => g.duplicate)).toEqual([false, true]);
	});
});

describe('SkillsView — rail filtering & sorting', () => {
	it('never lists a skill the player has not unlocked', () => {
		// Skill 4 is not owned, so it is absent from the rail (there is no longer a locked view).
		expect(view.railList.map((m) => m.skill.id)).not.toContain(4);
	});

	it('filters by attribute', () => {
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

	it('keeps the full equipped loadout in the rail when a search filter excludes it', () => {
		// 'delta' matches only the unequipped skill 3; the equipped rail must still show
		// the whole loadout (the header counts it) while the available rail is filtered.
		view.search = 'delta';
		expect(view.equippedRail.map((m) => m.skill.id)).toEqual([0, 1, 2]);
		expect(view.availableRail.map((m) => m.skill.id)).toEqual([3]);
	});

	it('keeps the equipped rail intact when an attribute filter matches no equipped skill', () => {
		// No fixture skill scales on Luck, so the attribute filter empties railList — the
		// equipped rail is built from the unfiltered loadout and must survive.
		view.toggleAttributeFilter(EAttribute.Luck);
		expect(view.equippedRail.map((m) => m.skill.id)).toEqual([0, 1, 2]);
		expect(view.availableRail).toEqual([]);
	});
});

describe('SkillsView — loadout edits persist the ordered loadout', () => {
	it('unequips a skill and sends the reduced loadout', async () => {
		view.toggle(0);
		expect(view.equipped).toEqual([1, 2]); // optimistic, synchronous
		// The persist is serialized, so the dispatch lands on a microtask.
		await vi.waitFor(() => expect(sendSocketCommand).toHaveBeenCalledWith('SetSelectedSkills', [1, 2]));
	});

	it('equips into a free slot and sends the extended loadout', async () => {
		view.toggle(0); // free a slot first (cap is 3)
		view.toggle(3);
		expect(view.equipped).toEqual([1, 2, 3]);
		await vi.waitFor(() => expect(lastPayload()).toEqual([1, 2, 3]));
	});

	it('starts a swap (no command) when equipping into a full loadout', () => {
		view.toggle(3);
		expect(view.pendingSwap).toBe(3);
		expect(view.equipped).toEqual([0, 1, 2]);
		expect(sendSocketCommand).not.toHaveBeenCalled();
	});

	it('resolves a swap by replacing the chosen slot', async () => {
		view.toggle(3); // full → pending swap
		view.swapInto(1);
		expect(view.equipped).toEqual([0, 3, 2]);
		expect(view.selectedId).toBe(3);
		expect(view.pendingSwap).toBeNull();
		await vi.waitFor(() => expect(lastPayload()).toEqual([0, 3, 2]));
	});

	it('reorders the loadout', async () => {
		view.reorder(0, 2);
		expect(view.equipped).toEqual([1, 2, 0]);
		await vi.waitFor(() => expect(lastPayload()).toEqual([1, 2, 0]));
	});

	it('ignores toggling a skill the player has not unlocked', () => {
		view.toggle(4); // not owned — the ownership guard rejects it
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

	it('keeps a later successful edit when an earlier overlapping commit fails', async () => {
		// Two rapid edits: the first persist fails, the second succeeds. Because commits are serialized
		// and only the latest edit's failure rolls back, the stale first failure must NOT clobber the
		// second edit (the bug this guards against).
		sendSocketCommand.mockReset();
		sendSocketCommand.mockResolvedValueOnce({ error: 'nope' }).mockResolvedValue({});
		view.toggle(0); // → [1, 2]
		view.toggle(3); // → [1, 2, 3] (the later, winning edit)
		await vi.waitFor(() => expect(sendSocketCommand).toHaveBeenCalledTimes(2));
		await vi.waitFor(() => expect(view.equipped).toEqual([1, 2, 3]));
		expect(mockPlayerManager.selectedSkills).toEqual([1, 2, 3]);
		expect(toastError).not.toHaveBeenCalled();
	});

	it('reverts only to the last confirmed loadout when the latest edit fails', async () => {
		view.toggle(0); // [0, 1, 2] → [1, 2], succeeds and is confirmed
		await vi.waitFor(() => expect(mockPlayerManager.selectedSkills).toEqual([1, 2]));
		sendSocketCommand.mockResolvedValueOnce({ error: 'nope' });
		view.reorder(0, 1); // [1, 2] → [2, 1], fails
		expect(view.equipped).toEqual([2, 1]); // optimistic
		await vi.waitFor(() => expect(toastError).toHaveBeenCalled());
		// Rolls back to the previously-confirmed [1, 2], not the original [0, 1, 2].
		expect(view.equipped).toEqual([1, 2]);
		expect(mockPlayerManager.selectedSkills).toEqual([1, 2]);
	});

	it('rolls back to an earlier overlapping commit that succeeded when the latest fails', async () => {
		// Two rapid overlapping edits: the FIRST persist succeeds (the server now holds [1, 2]), the
		// SECOND (latest) fails. The rollback target must be the server's actual current state — the
		// earlier success [1, 2] — NOT the pre-sequence loadout [0, 1, 2]. This is why a superseded
		// success must still advance `committed` (guards against the proposed "advance only for latest").
		sendSocketCommand.mockReset();
		sendSocketCommand.mockResolvedValueOnce({}).mockResolvedValueOnce({ error: 'nope' });
		view.toggle(0); // [0, 1, 2] → [1, 2] (succeeds, server-confirmed)
		view.toggle(3); // [1, 2] → [1, 2, 3] (latest, fails)
		await vi.waitFor(() => expect(sendSocketCommand).toHaveBeenCalledTimes(2));
		await vi.waitFor(() => expect(toastError).toHaveBeenCalled());
		expect(view.equipped).toEqual([1, 2]);
		expect(mockPlayerManager.selectedSkills).toEqual([1, 2]);
	});

	it('reconciles with the player manager once no edit is pending', async () => {
		// An external loadout change (e.g. a challenge skill unlock) lands while a commit is in flight;
		// once the queue drains the screen reflects the manager rather than the stale optimistic copy.
		// (In production the statified manager also propagates such changes live while idle.)
		view.toggle(0); // → [1, 2], in flight
		mockPlayerManager.setSelectedSkills([3]); // external write
		await vi.waitFor(() => expect(view.equipped).toEqual([3]));
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

describe('SkillsView — critical hits fold into effective damage', () => {
	beforeEach(() => {
		// Give the player crit chance + damage; the derived attribute pass also adds the base crit
		// damage (1.5) and any Dex/Luck contribution, so expectations are built off the view's resolved
		// values rather than hard-coding the derived model. Here: chance 0.5, damage 0.5 + 1.5 = 2.0.
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.CriticalChance, amount: 0.5 },
			{ attributeId: EAttribute.CriticalDamage, amount: 0.5 }
		];
		view = new SkillsView();
	});

	it('exposes a player-wide crit multiplier matching the shared display helper', () => {
		expect(view.critChance).toBeCloseTo(0.5, 10);
		expect(view.critDamage).toBeCloseTo(2, 10);
		expect(view.critMultiplier).toBe(expectedCritMultiplier(view.critChance, view.critDamage));
		expect(view.critMultiplier).toBeCloseTo(1.5, 10); // 1 + 0.5·(2−1)
	});

	it('leaves the multiplier at 1 when the player has no crit chance', () => {
		mockPlayerManager.attributes = [];
		const noCrit = new SkillsView();
		expect(noCrit.critChance).toBe(0);
		expect(noCrit.critMultiplier).toBe(1);
		expect(noCrit.critBonus(0)).toBe(0);
	});

	it('scales raw damage by the crit multiplier before defense in effective/dps/burst', () => {
		const k = view.critMultiplier;
		view.setDefense(10);
		// Alpha (id 0): baseDamage 12, no Strength allocated → rawDamage 12.
		expect(view.rawDamage(0)).toBe(12);
		expect(view.effective(0)).toBeCloseTo(Math.max(12 * k - 10, 0), 10); // (12·1.5−10)=8
		expect(view.effectiveDps(0)).toBeCloseTo(view.effective(0) / view.cooldown(0), 10);
		// Combined burst sums each loadout skill's crit-scaled effective hit.
		const expectedBurst = [0, 1, 2].reduce((sum, id) => sum + Math.max(view.rawDamage(id) * k - 10, 0), 0);
		expect(view.combinedEffectiveBurst).toBeCloseTo(expectedBurst, 10);
	});

	it('reports the expected crit bonus and clamps applied defense to the crit-scaled hit', () => {
		const k = view.critMultiplier;
		expect(view.critBonus(1)).toBeCloseTo(view.rawDamage(1) * (k - 1), 10); // Bravo: 40·0.5 = 20
		// A defense above the crit-scaled hit fully blocks it; applied defense clamps to that hit so the
		// breakdown's raw + crit − defense reconciles to 0.
		view.setDefense(1000);
		expect(view.appliedDefense(1)).toBeCloseTo(view.rawDamage(1) * k, 10);
		expect(view.effective(1)).toBe(0);
	});

	it('raises the slider ceiling to the biggest crit-scaled hit', () => {
		// Among the unlocked skills, Delta (id 3) has the largest raw damage (50); the ceiling scales
		// by the crit multiplier.
		expect(view.maxDamage).toBeCloseTo(50 * view.critMultiplier, 10);
	});
});
