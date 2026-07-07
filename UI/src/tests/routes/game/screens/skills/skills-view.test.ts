import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
	EDamageType,
	EModifierType,
	ERarity,
	EAttribute,
	ESkillAcquisition,
	type IChallenge,
	type IEnemy,
	type ISignaturePassive,
	type ISkill,
	type IZone
} from '$lib/api';
import { EAttributeModifierSource, type AttributeModifier } from '$lib/battle';
import { classSignaturePassiveModifier } from '$lib/battle/class-modifiers';

// Engine + stores are mocked so importing the view-model doesn't drag in the real
// game engine. `playerManager.unlockedSkills` is a plain writable array and
// `setSelectedSkills`/`selectedSkills` mirror the real PlayerManager (the view now
// routes loadout changes through the manager). `battleLockedBaseModifiers` /
// `battleSignaturePassiveModifier` / `playerProficiencies.battleModifiers` back the live battle-attribute
// composition (#1500) — reset to no-op defaults in `beforeEach`, overridden per test. `vi.hoisted` keeps
// these ready before the hoisted vi.mock factories run.
const { mockPlayerManager, mockInventoryManager, playerProficiencies, sendSocketCommand, toastError, staticData } =
	vi.hoisted(() => {
		const playerManager = {
			unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
			currentZone: 0,
			level: 1,
			attributes: [] as { attributeId: number; amount: number }[],
			battleLockedBaseModifiers: [] as AttributeModifier[],
			// Reassigned in beforeEach (and per-test) once ISignaturePassive/EModifierType are importable —
			// the placeholder keeps the property present for the hoisted factory.
			battleSignaturePassiveModifier: undefined as unknown as (
				resolve: (attribute: EAttribute) => number
			) => AttributeModifier,
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
				equippedSlots: [] as ({ grantedSkillId?: number; name: string } | undefined)[],
				grantedSkillIds: [] as number[],
				// The equipped weapon type driving the weapon-match grey-out (#1342); reset to Unarmed in beforeEach.
				// A numeric literal because this hoisted factory runs before the EDamageType import is evaluated.
				equippedWeaponType: 13 as EDamageType
			},
			playerProficiencies: { battleModifiers: [] as AttributeModifier[] },
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
vi.mock('$stores', () => ({ staticData, toastError, playerProficiencies }));
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
	criticalChance: 0,
	damageMultipliers: [],
	effects: [],
	description: '',
	designerNotes: '',
	cooldownMs: 1000,
	iconPath: '',
	rarityId: ERarity.Common,
	word: '',
	pronunciation: '',
	translation: '',
	damagePortions: [{ type: EDamageType.Physical, weight: 1 }],
	acquisition: ESkillAcquisition.Player,
	...over
});

const metric = (over: Partial<SkillMetrics> & { skill: ISkill }): SkillMetrics => ({
	rawDamage: 0,
	cooldown: 1,
	contributions: [],
	critChance: 0,
	critMultiplier: 1,
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

/* Compare-vs fixtures. Enemy Toughness resolves through the real BattleAttributes as
   `2·Endurance` (no base, no Agility term), so each enemy's distribution is chosen for a
   clean expected value: with only per-level Endurance, Toughness = 2·amountPerLevel·level. */
const enemy = (over: Partial<IEnemy> & { id: number }): IEnemy => ({
	name: `Enemy ${over.id}`,
	designerNotes: '',
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
		designerNotes: '',
		order: 0,
		levelMin: 2,
		levelMax: 8,
		bossEnemyId: 2,
		bossLevel: 10,
		isHome: false
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

// A flat no-op signature passive — the default a player whose class has no passive carries. It
// contributes nothing, so it doesn't perturb the raw-damage assertions below unless a test overrides it.
const noOpPassive: ISignaturePassive = {
	attributeId: EAttribute.Strength,
	amount: 0,
	scalingAmount: 0,
	modifierType: EModifierType.Additive
};

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
	mockPlayerManager.battleLockedBaseModifiers = [];
	mockPlayerManager.battleSignaturePassiveModifier = (resolve) => classSignaturePassiveModifier(noOpPassive, resolve);
	mockInventoryManager.equipmentStats = [];
	mockInventoryManager.equippedSlots = [];
	mockInventoryManager.grantedSkillIds = [];
	mockInventoryManager.equippedWeaponType = EDamageType.Unarmed;
	playerProficiencies.battleModifiers = [];
	view = new SkillsView();
});

/* The raw-damage / toughness-mitigation / cooldown-multiplier formulas are the shared battle
   formulas (`$lib/battle/battle-formulas`), unit-tested in `tests/lib/battle/battle-formulas.test.ts`;
   here only the page-local helpers and the view's consumption of the shared ones are covered. */
describe('pure helpers', () => {
	it('returns zero dps for a non-positive cooldown', () => {
		expect(damagePerSecond(50, 2)).toBe(25);
		expect(damagePerSecond(50, 0)).toBe(0);
	});

	it('sorts by name, ascending cooldown, and toughness-aware dps/damage', () => {
		const a = metric({ skill: skill({ id: 0, name: 'Zeta' }), rawDamage: 100, cooldown: 2 });
		const b = metric({ skill: skill({ id: 1, name: 'Alpha' }), rawDamage: 30, cooldown: 1 });

		expect([a, b].sort(sortMetrics('name', 0)).map((m) => m.skill.name)).toEqual(['Alpha', 'Zeta']);
		expect([a, b].sort(sortMetrics('cd', 0)).map((m) => m.skill.id)).toEqual([1, 0]);
		// dps with no toughness: a=100/2=50, b=30/1=30 → a first.
		expect([b, a].sort(sortMetrics('dps', 0)).map((m) => m.skill.id)).toEqual([0, 1]);
		// damage with toughness 80 (factor 200/280≈0.714): a=100·0.714≈71.4, b=30·0.714≈21.4 → a first.
		expect([b, a].sort(sortMetrics('dmg', 80)).map((m) => m.skill.id)).toEqual([0, 1]);
	});

	it('takes the midpoint of a zone range as the representative spawn level', () => {
		expect(zoneSpawnLevel(ZONES[0])).toBe(5); // (2 + 8) / 2
	});

	it('folds each metric’s own crit multiplier into the effective dps/damage ranking', () => {
		// Each SkillMetrics carries its own crit multiplier (#1453 — the enabler is the skill's own base
		// chance, not a build-wide stat), scaling raw damage before the Toughness curve, mirroring the battle
		// and the displayed numbers. The curve is multiplicative, so a UNIFORM crit multiplier across both
		// rows scales their effective values by the same factor — it preserves dps ordering rather than
		// flipping it.
		const a = metric({ skill: skill({ id: 0 }), rawDamage: 30, cooldown: 3 });
		const b = metric({ skill: skill({ id: 1 }), rawDamage: 22, cooldown: 2 });
		// toughness 100 (factor 200/300 = 2/3), no crit: A=30·(2/3)/3≈6.67 < B=22·(2/3)/2≈7.33 → B first.
		expect([b, a].sort(sortMetrics('dps', 100)).map((m) => m.skill.id)).toEqual([1, 0]);
		// crit ×2 on both rows: both effective values double, so the order is unchanged → B still first.
		const aCrit = metric({ skill: skill({ id: 0 }), rawDamage: 30, cooldown: 3, critMultiplier: 2 });
		const bCrit = metric({ skill: skill({ id: 1 }), rawDamage: 22, cooldown: 2, critMultiplier: 2 });
		expect([aCrit, bCrit].sort(sortMetrics('dps', 100)).map((m) => m.skill.id)).toEqual([1, 0]);
		// dmg sort, toughness 250 (factor 200/450≈0.44), crit ×2: A=60·0.44≈26.7 > B=44·0.44≈19.6 → A first.
		expect([aCrit, bCrit].sort(sortMetrics('dmg', 250)).map((m) => m.skill.id)).toEqual([0, 1]);
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

	it('exposes the toughest compare-vs preset as the slider ceiling', () => {
		expect(view.maxToughness).toBe(60); // Ogre King boss Toughness (2·Endurance 30) — the hardest target
	});

	it('resolves metrics through the shared battle formulas and the derived attribute composition', () => {
		// Strength feeds Alpha's 1× multiplier directly. CDR is severed from the core attributes (#1426): the
		// effective charge rate is CooldownRecovery(1) + CooldownBonus × CooldownBonusMultiplier, where an
		// authored CooldownBonus is the enabler and Agility (0.002/pt) lifts the multiplier.
		mockPlayerManager.attributes = [
			{ attributeId: EAttribute.Strength, amount: 20 },
			{ attributeId: EAttribute.Agility, amount: 10 },
			{ attributeId: EAttribute.CooldownBonus, amount: 0.5 }
		];
		const fresh = new SkillsView();
		expect(fresh.rawDamage(0)).toBe(32); // 12 base + 20·1
		// cdMult = 1 + 0.5·(1 + 0.002·10) = 1 + 0.5·1.02 = 1.51, read as the cooldown multiplier.
		expect(fresh.cooldown(0)).toBeCloseTo(1 / 1.51, 10);
		expect(fresh.metric(0)?.contributions).toEqual([{ attributeId: EAttribute.Strength, multiplier: 1, value: 20 }]);
	});

	it('folds the class locked base into battle numbers, matching the live battler (#1500)', () => {
		mockPlayerManager.battleLockedBaseModifiers = [
			{
				attribute: EAttribute.Strength,
				amount: 15,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.AttributeDistribution
			}
		];
		const fresh = new SkillsView();
		expect(fresh.rawDamage(0)).toBe(27); // Alpha: 12 base + 15 locked-base Strength
	});

	it('folds proficiency bonuses into battle numbers, matching the live battler (#1500)', () => {
		playerProficiencies.battleModifiers = [
			{
				attribute: EAttribute.Strength,
				amount: 5,
				type: EModifierType.Additive,
				source: EAttributeModifierSource.Proficiency
			}
		];
		const fresh = new SkillsView();
		expect(fresh.rawDamage(0)).toBe(17); // Alpha: 12 base + 5 proficiency Strength
	});

	it('folds the class signature passive into battle numbers, matching the live battler (#1500)', () => {
		mockPlayerManager.battleSignaturePassiveModifier = () => ({
			attribute: EAttribute.Strength,
			amount: 8,
			type: EModifierType.Additive,
			source: EAttributeModifierSource.Class
		});
		const fresh = new SkillsView();
		expect(fresh.rawDamage(0)).toBe(20); // Alpha: 12 base + 8 signature passive Strength
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
		mockInventoryManager.grantedSkillIds = [3, 4];
		const fresh = new SkillsView();

		expect(fresh.innateSkills.map((g) => [g.skill.id, g.sourceItemName, g.duplicate])).toEqual([
			[3, 'Helm of Echoes', false],
			[4, 'Staff of Embers', false]
		]);
	});

	it('flags a grant that duplicates a selected skill as already in the loadout', () => {
		// Skill 0 is in the equipped loadout, so an item granting it is a duplicate (fielded once).
		mockInventoryManager.equippedSlots = [{ name: 'Echo Blade', grantedSkillId: 0 }];
		mockInventoryManager.grantedSkillIds = [0];
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
		mockInventoryManager.grantedSkillIds = [3];
		const fresh = new SkillsView();

		expect(fresh.innateSkills.map((g) => g.duplicate)).toEqual([false, true]);
	});

	it('resolves metrics for a granted skill the player has not separately unlocked (#1500)', () => {
		// Skill 4 is not in unlockedSkills (never owned/unlocked directly) — only granted by the item.
		// Before #1500 the innate band's read-only card would silently lose its dmg/cd/dps block here.
		mockInventoryManager.equippedSlots = [{ name: 'Staff of Embers', grantedSkillId: 4 }];
		mockInventoryManager.grantedSkillIds = [4];
		const fresh = new SkillsView();

		expect(fresh.catalogue.map((s) => s.id)).not.toContain(4); // still excluded from the unlocked catalogue
		expect(fresh.metric(4)).toBeDefined();
		expect(fresh.metric(4)?.rawDamage).toBe(100); // Echo's base damage, no scaling attribute allocated
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

	it('ranks by effective dps, with the multiplicative curve preserving order under toughness', () => {
		view.setSort('dps');
		// no toughness: dps = base/cd → Delta(50) > Bravo(20) > Alpha(12) > Charlie(10)
		expect(view.railList.map((m) => m.skill.id)).toEqual([3, 1, 0, 2]);
		// toughness 15 scales every hit by the same factor (200/215), so the dps order is unchanged.
		view.setToughness(15);
		expect(view.railList.map((m) => m.skill.id)).toEqual([3, 1, 0, 2]);
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

describe('SkillsView — compare-vs toughness', () => {
	it('clamps the toughness to the slider range', () => {
		view.setToughness(-5);
		expect(view.toughness).toBe(0);
		view.setToughness(9999);
		expect(view.toughness).toBe(view.maxToughness);
	});

	it('reports combined effective dps/burst over the equipped loadout', () => {
		// base damages of [0,1,2] = 12 + 40 + 5 = 57 at zero toughness (no mitigation).
		expect(Math.round(view.combinedEffectiveBurst)).toBe(57);
		expect(view.combinedEffectiveDps).toBeGreaterThan(0);
	});
});

describe('SkillsView — compare-vs enemy presets', () => {
	it('sources pills from the current zone: in-zone spawns (midpoint level) plus the boss', () => {
		const presets = view.comparePresets;
		// Imp spawns in zone 0; Wolf spawns elsewhere; Wraith is retired; Ogre King is the boss.
		expect(presets.map((p) => p.key)).toEqual(['spawn-0', 'boss-2']);
		expect(presets[0]).toMatchObject({ name: 'Imp', isBoss: false, toughness: 20 }); // 2·(2·5)
		expect(presets[1]).toMatchObject({ name: 'Ogre King', isBoss: true, toughness: 60 }); // 2·(3·10)
	});

	it('is empty when the current zone has no reference data yet', () => {
		staticData.zones = undefined;
		const fresh = new SkillsView();
		expect(fresh.comparePresets).toEqual([]);
	});

	it('snaps the slider to a preset and marks its pill selected', () => {
		const [spawn, boss] = view.comparePresets;
		view.selectPreset(spawn);
		expect(view.toughness).toBe(20);
		expect(view.selectedPresetKey).toBe('spawn-0');

		view.selectPreset(boss);
		expect(view.toughness).toBe(60);
		expect(view.selectedPresetKey).toBe('boss-2');
	});

	it('clamps a preset toughness above the slider ceiling but keeps the pill selected', () => {
		view.selectPreset({ key: 'spawn-9', name: 'Colossus', isBoss: false, toughness: 9999 });
		expect(view.toughness).toBe(view.maxToughness);
		expect(view.selectedPresetKey).toBe('spawn-9');
	});

	it('deselects the active pill when the slider is dragged manually', () => {
		view.selectPreset(view.comparePresets[0]);
		expect(view.selectedPresetKey).toBe('spawn-0');
		view.setToughness(7);
		expect(view.toughness).toBe(7);
		expect(view.selectedPresetKey).toBeNull();
	});

	it('resorts railList by effective dps when a preset is selected', () => {
		view.setSort('dps');
		// At 0 toughness: Delta(50/1=50) > Bravo(40/2=20) > Alpha(12/1=12) > Charlie(5/0.5=10).
		expect(view.railList.map((m) => m.skill.id)).toEqual([3, 1, 0, 2]);

		// Imp (toughness=20, level 1 → factor 20/40=0.5): every hit halves, so the order is unchanged.
		const [spawn] = view.comparePresets;
		view.selectPreset(spawn);
		expect(view.toughness).toBe(spawn.toughness);
		expect(view.railList.map((m) => m.skill.id).slice(0, 2)).toEqual([3, 1]);
	});
});

describe('SkillsView — critical hits fold into effective damage', () => {
	beforeEach(() => {
		// Each skill's own base CriticalChance is the opt-in enabler (#1453), not a build-wide stat, so
		// every skill in the shared catalogue is given the same 0.5 base to keep these uniform-crit
		// assertions as before. CriticalChanceMultiplier defaults to its base of 1 (no investment), so the
		// resolved chance is exactly the skill's own 0.5. CriticalDamage is still page-wide: the derived
		// attribute pass adds its base (1.5) to the 0.5 allocation, so expectations are built off the
		// view's resolved values rather than hard-coding the derived model. Here: damage 0.5 + 1.5 = 2.0.
		staticData.skills = SKILLS.map((s) => ({ ...s, criticalChance: 0.5 }));
		mockPlayerManager.attributes = [{ attributeId: EAttribute.CriticalDamage, amount: 0.5 }];
		view = new SkillsView();
	});

	it('exposes each skill’s effective crit chance/multiplier matching the shared display helper', () => {
		const alpha = view.metric(0);
		expect(alpha?.critChance).toBeCloseTo(0.5, 10);
		expect(view.critDamage).toBeCloseTo(2, 10);
		expect(alpha?.critMultiplier).toBe(expectedCritMultiplier(alpha!.critChance, view.critDamage));
		expect(alpha?.critMultiplier).toBeCloseTo(1.5, 10); // 1 + 0.5·(2−1)
	});

	it('leaves the multiplier at 1 when a skill has no crit chance', () => {
		staticData.skills = SKILLS; // the default catalogue's skills all backfill to 0
		const noCrit = new SkillsView();
		expect(noCrit.metric(0)?.critChance).toBe(0);
		expect(noCrit.metric(0)?.critMultiplier).toBe(1);
		expect(noCrit.critBonus(0)).toBe(0);
	});

	it('scales raw damage by each skill’s own crit multiplier before the toughness curve in effective/dps/burst', () => {
		const k = view.metric(0)!.critMultiplier;
		view.setToughness(50);
		// Toughness 50 (within the preset-capped slider range) → curve factor 200/(50+200) = 0.8.
		const curve = 200 / (50 + 200);
		// Alpha (id 0): baseDamage 12, no Strength allocated → rawDamage 12.
		expect(view.rawDamage(0)).toBe(12);
		expect(view.effective(0)).toBeCloseTo(12 * k * curve, 10); // 12·1.5·0.8 = 14.4
		expect(view.effectiveDps(0)).toBeCloseTo(view.effective(0) / view.cooldown(0), 10);
		// Combined burst sums each loadout skill's own crit-scaled, curve-mitigated hit (uniform crit here,
		// so it reduces to the same k for every id).
		const expectedBurst = [0, 1, 2].reduce((sum, id) => sum + view.rawDamage(id) * k * curve, 0);
		expect(view.combinedEffectiveBurst).toBeCloseTo(expectedBurst, 10);
	});

	it('reports the expected crit bonus and the curve-mitigated amount of the crit-scaled hit', () => {
		const k = view.metric(1)!.critMultiplier;
		expect(view.critBonus(1)).toBeCloseTo(view.rawDamage(1) * (k - 1), 10); // Bravo: 40·0.5 = 20
		// The curve asymptotes, so even overwhelming toughness leaves a positive sliver through — it never
		// fully blocks. mitigatedAmount is the crit-scaled hit minus that remaining effective damage.
		view.setToughness(1000);
		expect(view.effective(1)).toBeGreaterThan(0);
		expect(view.mitigatedAmount(1)).toBeCloseTo(view.rawDamage(1) * k - view.effective(1), 10);
	});

	it('caps the slider ceiling at the toughest compare-vs preset', () => {
		// The ceiling is the hardest current target's Toughness (Ogre King boss = 60), independent of the
		// crit multiplier — the curve has no "block everything" point to scale toward.
		expect(view.maxToughness).toBe(60);
	});
});

/* The header's combined effective totals must never sum an output the battle can't produce (#1637):
   a dormant (weapon-mismatched) equipped skill is excluded, mirroring the per-card suppression
   `EquippedBand.svelte` already applies. */
describe('SkillsView — combined effective totals exclude dormant skills (#1637)', () => {
	const swordSkill = skill({
		id: 6,
		name: 'Golf',
		baseDamage: 20,
		cooldownMs: 1000,
		damagePortions: [{ type: EDamageType.Sword, weight: 1 }]
	});

	beforeEach(() => {
		staticData.skills = [...SKILLS, swordSkill];
		mockPlayerManager.unlockedSkills = [
			{ skillId: 0, selected: true, order: 0 },
			{ skillId: 1, selected: true, order: 1 },
			{ skillId: 6, selected: true, order: 2 }
		];
	});

	it('contributes 0 to both totals while dormant under a mismatched weapon', () => {
		mockInventoryManager.equippedWeaponType = EDamageType.Unarmed;
		const v = new SkillsView();
		expect(v.dormant(swordSkill)).toBe(true);
		expect(v.combinedEffectiveBurst).toBeCloseTo(v.effective(0) + v.effective(1), 10);
		expect(v.combinedEffectiveDps).toBeCloseTo(v.effectiveDps(0) + v.effectiveDps(1), 10);
	});

	it('restores its contribution once the matching weapon is equipped', () => {
		mockInventoryManager.equippedWeaponType = EDamageType.Sword;
		const v = new SkillsView();
		expect(v.dormant(swordSkill)).toBe(false);
		expect(v.combinedEffectiveBurst).toBeCloseTo(v.effective(0) + v.effective(1) + v.effective(6), 10);
		expect(v.combinedEffectiveDps).toBeCloseTo(v.effectiveDps(0) + v.effectiveDps(1) + v.effectiveDps(6), 10);
	});
});

/* The weapon-match grey-out (#1342): a weapon-leaf-typed skill is dormant unless the matching weapon is
   equipped. `dormant()` derives from the same `isFielded` rule the battle assembly applies. */
describe('SkillsView — weapon-match grey-out', () => {
	const typed = (type: EDamageType): ISkill => skill({ id: 99, damagePortions: [{ type, weight: 1 }] });

	/** A fresh view that reads `weaponType` as the equipped weapon (the derived caches on construction). */
	const viewWithWeapon = (weaponType: EDamageType): SkillsView => {
		mockInventoryManager.equippedWeaponType = weaponType;
		return new SkillsView();
	};

	it('dims a weapon-leaf skill that does not match the equipped weapon', () => {
		const v = viewWithWeapon(EDamageType.Sword);
		expect(v.dormant(typed(EDamageType.Axe))).toBe(true);
		expect(v.dormant(typed(EDamageType.Sword))).toBe(false);
	});

	it('bare-handed (Unarmed) fields only Unarmed weapon-leaf skills', () => {
		const v = viewWithWeapon(EDamageType.Unarmed);
		expect(v.dormant(typed(EDamageType.Unarmed))).toBe(false);
		expect(v.dormant(typed(EDamageType.Sword))).toBe(true);
	});

	it('never dims weapon-agnostic skills', () => {
		const v = viewWithWeapon(EDamageType.Sword);
		expect(v.dormant(typed(EDamageType.Physical))).toBe(false);
		expect(v.dormant(typed(EDamageType.Fire))).toBe(false);
	});
});
