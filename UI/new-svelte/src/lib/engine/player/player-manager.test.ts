import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('../log', () => ({
	logMessage: vi.fn()
}));

import { PlayerManager } from './player-manager';
import { logMessage } from '../log';
import { EAttribute, ELogType } from '$lib/api';
import type { IPlayerData } from '$lib/api';

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
	selectedSkills: [0, 1, 2],
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
				appliedMods: [{ itemModId: 10, itemModSlotId: 0 }]
			},
			{
				itemId: 2,
				equipped: false,
				equipmentSlotId: null,
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
			expect(manager.selectedSkills).toEqual([0, 1, 2]);
			expect(manager.logPreferences).toEqual(data.logPreferences);
			expect(manager.inventoryData).toEqual(data.inventoryData);
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

	describe('default state', () => {
		it('starts with empty defaults before initialization', () => {
			expect(manager.name).toBe('');
			expect(manager.level).toBe(0);
			expect(manager.exp).toBe(0);
			expect(manager.attributes).toEqual([]);
			expect(manager.selectedSkills).toEqual([]);
			expect(manager.logPreferences).toEqual([]);
			expect(manager.inventoryData).toEqual({
				unlockedItems: [],
				unlockedMods: []
			});
		});
	});
});
