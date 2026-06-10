import { describe, it, expect, vi, beforeEach } from 'vitest';
import { EAttribute, type IChallenge, type ISkill } from '$lib/api';
import type { BattleAttributes } from '$lib/battle';

// Engine + stores are mocked so importing the view-model doesn't drag in the real
// game engine. `playerManager.unlockedSkills` is a plain writable array (the view
// reassigns it on persist) and `selectedSkills` derives the equipped order from it,
// mirroring the real PlayerManager. `vi.hoisted` keeps these ready before the
// hoisted vi.mock factories run.
const { mockPlayerManager, mockInventoryManager, sendSocketCommand, toastError, staticData } = vi.hoisted(() => {
	const playerManager = {
		unlockedSkills: [] as { skillId: number; selected: boolean; order?: number }[],
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
		staticData: { skills: [] as ISkill[], challenges: [] as IChallenge[], attributes: undefined as unknown }
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
	effectiveDamage,
	damagePerSecond,
	skillRawDamage,
	skillContributions,
	sortMetrics,
	type SkillMetrics
} from '$routes/game/screens/skills/skills-view.svelte';

/* A skill with sensible defaults so each test only states what matters. */
const skill = (over: Partial<ISkill> & { id: number }): ISkill => ({
	name: `Skill ${over.id}`,
	baseDamage: 0,
	damageMultipliers: [],
	description: '',
	cooldownMs: 1000,
	iconPath: '',
	...over
});

/** A fake `BattleAttributes` that returns canned values per attribute id. */
const fakeAttributes = (values: Partial<Record<EAttribute, number>>): BattleAttributes =>
	({ getValue: (id: EAttribute) => values[id] ?? 0 }) as unknown as BattleAttributes;

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

const lastPayload = (): number[] => sendSocketCommand.mock.calls.at(-1)?.[1] as number[];

let view: SkillsView;

beforeEach(() => {
	sendSocketCommand.mockReset().mockResolvedValue({});
	toastError.mockReset();
	staticData.skills = SKILLS;
	staticData.challenges = CHALLENGES;
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

describe('pure helpers', () => {
	it('clamps effective damage at zero', () => {
		expect(effectiveDamage(30, 10)).toBe(20);
		expect(effectiveDamage(10, 40)).toBe(0);
	});

	it('returns zero dps for a non-positive cooldown', () => {
		expect(damagePerSecond(50, 2)).toBe(25);
		expect(damagePerSecond(50, 0)).toBe(0);
	});

	it('computes raw damage as base plus each attribute contribution', () => {
		const s = skill({
			id: 9,
			baseDamage: 10,
			damageMultipliers: [
				{ attributeId: EAttribute.Strength, multiplier: 2 },
				{ attributeId: EAttribute.Luck, multiplier: 0.5 }
			]
		});
		const attrs = fakeAttributes({ [EAttribute.Strength]: 20, [EAttribute.Luck]: 8 });
		// 10 + 20*2 + 8*0.5 = 54
		expect(skillRawDamage(s, attrs)).toBe(54);
	});

	it('breaks down per-attribute contributions', () => {
		const s = skill({
			id: 9,
			baseDamage: 10,
			damageMultipliers: [{ attributeId: EAttribute.Strength, multiplier: 2 }]
		});
		const attrs = fakeAttributes({ [EAttribute.Strength]: 20 });
		expect(skillContributions(s, attrs)).toEqual([{ attributeId: EAttribute.Strength, multiplier: 2, value: 40 }]);
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
