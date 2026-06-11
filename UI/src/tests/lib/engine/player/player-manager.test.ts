import { describe, it, expect, vi, beforeEach } from 'vitest';
import { PlayerManager } from '$lib/engine/player/player-manager';
import { logMessage } from '$lib/engine/log';
import { EAttribute, ELogType } from '$lib/api';
import type { IPlayerData } from '$lib/api';

// PlayerManager only depends on logMessage; mock it so the unit under test is
// isolated and the spy assertions below work (mirrors inventory-manager.test.ts).
vi.mock('$lib/engine/log', () => ({
	logMessage: vi.fn()
}));

const makePlayerData = (overrides: Partial<IPlayerData> = {}): IPlayerData => ({
	name: 'TestPlayer',
	level: 5,
	exp: 200,
	currentZone: 2,
	statPointsGained: 30,
	statPointsUsed: 24,
	attributes: [
		{ attributeId: EAttribute.Strength, amount: 10 },
		{ attributeId: EAttribute.Endurance, amount: 8 }
	],
	unlockedSkills: [
		{ skillId: 0, selected: true, order: 1 },
		{ skillId: 1, selected: true, order: 0 },
		{ skillId: 2, selected: false },
		{ skillId: 3, selected: true, order: 2 }
	],
	logPreferences: [
		{ id: ELogType.Damage, enabled: false },
		{ id: ELogType.Exp, enabled: true }
	],
	inventoryData: {
		unlockedItems: [
			{
				itemId: 1,
				equipped: true,
				equipmentSlotId: 4,
				favorite: false,
				appliedMods: [{ itemModId: 10, itemModSlotId: 0 }]
			},
			{
				itemId: 2,
				equipped: false,
				favorite: false,
				appliedMods: []
			}
		],
		unlockedMods: [10, 11, 12]
	},
	...overrides
});

describe('PlayerManager', () => {
	let manager: PlayerManager;

	beforeEach(() => {
		manager = new PlayerManager();
		vi.mocked(logMessage).mockClear();
	});

	describe('initialize', () => {
		it('copies all fields from player data', () => {
			const data = makePlayerData();
			manager.initialize(data);

			expect(manager.name).toBe('TestPlayer');
			expect(manager.level).toBe(5);
			expect(manager.exp).toBe(200);
			expect(manager.currentZone).toBe(2);
			expect(manager.statPointsGained).toBe(30);
			expect(manager.statPointsUsed).toBe(24);
			expect(manager.attributes).toEqual(data.attributes);
			expect(manager.unlockedSkills).toEqual(data.unlockedSkills);
			expect(manager.logPreferences).toEqual(data.logPreferences);
			expect(manager.inventoryData).toEqual(data.inventoryData);
		});

		it('derives selectedSkills as the equipped ids in loadout order', () => {
			manager.initialize(makePlayerData());

			// Equipped skills 1 (order 0), 0 (order 1), 3 (order 2); skill 2 is unequipped.
			expect(manager.selectedSkills).toEqual([1, 0, 3]);
		});

		it('overwrites previous state on re-initialize', () => {
			manager.initialize(makePlayerData({ name: 'First', level: 1 }));
			manager.initialize(makePlayerData({ name: 'Second', level: 10 }));

			expect(manager.name).toBe('Second');
			expect(manager.level).toBe(10);
		});
	});

	describe('grantExp', () => {
		it('adds exp and logs the message', () => {
			manager.initialize(makePlayerData({ level: 1, exp: 0 }));
			manager.grantExp(50);

			expect(manager.exp).toBe(50);
			expect(logMessage).toHaveBeenCalledWith(ELogType.Exp, 'Earned 50 exp.');
		});

		it('triggers levelUp when exp exceeds threshold', () => {
			manager.initialize(makePlayerData({ level: 1, exp: 0 }));
			manager.grantExp(100);

			expect(manager.level).toBe(2);
			expect(manager.exp).toBe(0);
			expect(manager.statPointsGained).toBe(36);
		});

		it('does not level up when exp is below threshold', () => {
			manager.initialize(makePlayerData({ level: 2, exp: 0 }));
			manager.grantExp(199);

			expect(manager.level).toBe(2);
			expect(manager.exp).toBe(199);
		});
	});

	describe('levelUp', () => {
		it('increments level and grants 6 stat points', () => {
			manager.initialize(makePlayerData({ level: 3, exp: 300, statPointsGained: 12 }));
			manager.levelUp();

			expect(manager.level).toBe(4);
			expect(manager.exp).toBe(0);
			expect(manager.statPointsGained).toBe(18);
		});

		it('logs level-up messages', () => {
			manager.initialize(makePlayerData({ level: 1, exp: 100 }));
			manager.levelUp();

			expect(logMessage).toHaveBeenCalledWith(ELogType.LevelUp, 'Congratulations, you leveled up!');
			expect(logMessage).toHaveBeenCalledWith(ELogType.LevelUp, 'You are now level 2.');
		});
	});

	describe('addUnlockedSkill', () => {
		it('adds the skill to the unlocked set unselected and logs it', () => {
			manager.initialize(makePlayerData({ unlockedSkills: [{ skillId: 0, selected: true, order: 0 }] }));

			manager.addUnlockedSkill(5);

			expect(manager.unlockedSkills).toContainEqual({ skillId: 5, selected: false });
			// Earning a skill does not equip it.
			expect(manager.selectedSkills).not.toContain(5);
			expect(logMessage).toHaveBeenCalledWith(ELogType.ItemFound, 'New skill unlocked!');
		});

		it('reassigns the array so reactive consumers re-derive', () => {
			manager.initialize(makePlayerData({ unlockedSkills: [] }));
			const before = manager.unlockedSkills;

			manager.addUnlockedSkill(5);

			expect(manager.unlockedSkills).not.toBe(before);
		});

		it('is idempotent — an already-unlocked skill is not duplicated', () => {
			manager.initialize(makePlayerData({ unlockedSkills: [{ skillId: 5, selected: false }] }));

			manager.addUnlockedSkill(5);

			expect(manager.unlockedSkills.filter((s) => s.skillId === 5)).toHaveLength(1);
			expect(logMessage).not.toHaveBeenCalled();
		});
	});

	describe('default state', () => {
		it('starts with empty defaults before initialization', () => {
			expect(manager.name).toBe('');
			expect(manager.level).toBe(0);
			expect(manager.exp).toBe(0);
			expect(manager.attributes).toEqual([]);
			expect(manager.unlockedSkills).toEqual([]);
			expect(manager.selectedSkills).toEqual([]);
			expect(manager.logPreferences).toEqual([]);
			expect(manager.inventoryData).toEqual({
				unlockedItems: [],
				unlockedMods: []
			});
		});
	});
});
