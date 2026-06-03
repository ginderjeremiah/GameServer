import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ELogType } from '$lib/api';
import type { ISkill } from '$lib/api';

vi.mock('svelte', async (importOriginal) => ({
	...((await importOriginal()) as Record<string, unknown>),
	onDestroy: vi.fn()
}));

const { mockSkills, mockEnemies, mockPlayerManager, mockInventoryManager } = vi.hoisted(() => {
	const mockSkills: ISkill[] = [];
	const mockEnemies: any[] = [];
	const mockPlayerManager = {
		name: 'TestPlayer',
		level: 5,
		selectedSkills: [0],
		attributes: [
			{ attributeId: 0, amount: 50 },
			{ attributeId: 1, amount: 30 }
		]
	};
	const mockInventoryManager = {
		equipmentStats: []
	};

	return { mockSkills, mockEnemies, mockPlayerManager, mockInventoryManager };
});

let { logicalUpdateCallbacks, renderUpdateCallbacks, enemyLoadedCallbacks } = vi.hoisted(() => {
	const logicalUpdateCallbacks: Function[] = [];
	const renderUpdateCallbacks: Function[] = [];
	const enemyLoadedCallbacks: Function[] = [];

	return { logicalUpdateCallbacks, renderUpdateCallbacks, enemyLoadedCallbacks };
});

vi.mock('$stores', () => ({
	staticData: {
		get skills() {
			return mockSkills;
		},
		get enemies() {
			return mockEnemies;
		}
	}
}));

vi.mock('$lib/engine/log', () => ({
	logMessage: vi.fn()
}));

vi.mock('$lib/engine/engine', () => ({
	get inventoryManager() {
		return mockInventoryManager;
	}
}));

vi.mock('$lib/engine/player/player-manager', () => ({
	get playerManager() {
		return mockPlayerManager;
	}
}));

vi.mock('$lib/engine/logical-engine', () => ({
	LogicalEngine: vi.fn(class {}),
	onLogicalUpdate: vi.fn((cb: Function) => {
		logicalUpdateCallbacks.push(cb);
		return () => {
			logicalUpdateCallbacks = logicalUpdateCallbacks.filter((c) => c !== cb);
		};
	})
}));

vi.mock('$lib/engine/render-engine', () => ({
	RenderEngine: vi.fn(class {}),
	onRenderUpdate: vi.fn((cb: Function) => {
		renderUpdateCallbacks.push(cb);
		return () => {
			renderUpdateCallbacks = renderUpdateCallbacks.filter((c) => c !== cb);
		};
	})
}));

vi.mock('$lib/engine/battle/enemy-manager', () => ({
	onNewEnemyLoaded: vi.fn((cb: Function) => {
		enemyLoadedCallbacks.push(cb);
		return () => {
			enemyLoadedCallbacks = enemyLoadedCallbacks.filter((c) => c !== cb);
		};
	})
}));

import { BattleEngine, BattleStage } from '$lib/engine/battle/battle-engine';
import { logMessage } from '$lib/engine/log';

describe('BattleEngine', () => {
	let engine: BattleEngine;

	beforeEach(() => {
		logicalUpdateCallbacks = [];
		renderUpdateCallbacks = [];
		enemyLoadedCallbacks = [];
		vi.mocked(logMessage).mockClear();

		mockSkills.length = 0;
		mockSkills[0] = {
			id: 0,
			name: 'Slash',
			baseDamage: 100,
			damageMultipliers: [],
			description: '',
			cooldownMs: 500,
			iconPath: ''
		};

		mockEnemies.length = 0;
		mockEnemies[1] = { name: 'Goblin', selectedSkills: [0], attributes: [] };

		engine = new BattleEngine();
	});

	describe('start/stop lifecycle', () => {
		it('starts in Idle stage', () => {
			expect(engine.stage).toBe(BattleStage.Idle);
		});

		it('registers hooks on start', () => {
			engine.start();

			expect(logicalUpdateCallbacks).toHaveLength(1);
			expect(renderUpdateCallbacks).toHaveLength(1);
			expect(enemyLoadedCallbacks).toHaveLength(1);
		});

		it('removes hooks on stop', () => {
			engine.start();
			engine.stop();

			expect(logicalUpdateCallbacks).toHaveLength(0);
			expect(renderUpdateCallbacks).toHaveLength(0);
			expect(enemyLoadedCallbacks).toHaveLength(0);
		});

		it('start is idempotent', () => {
			engine.start();
			engine.start();

			expect(logicalUpdateCallbacks).toHaveLength(1);
		});

		it('sets running flag', () => {
			expect(engine.running).toBe(false);
			engine.start();
			expect(engine.running).toBe(true);
			engine.stop();
			expect(engine.running).toBe(false);
		});
	});

	describe('battle state transitions', () => {
		it('transitions to Active when enemy is loaded', () => {
			engine.start();

			const enemyInstance = { id: 1, level: 1, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			expect(engine.stage).toBe(BattleStage.Active);
		});

		it('transitions to Victorious when enemy dies', () => {
			engine.start();

			const enemyInstance = { id: 1, level: 1, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			// Simulate enough logical updates to kill the enemy
			for (let i = 0; i < 100; i++) {
				if (engine.stage === BattleStage.Active) {
					logicalUpdateCallbacks[0](40);
				}
				if (engine.stage !== BattleStage.Active) break;
			}

			expect(engine.stage).toBe(BattleStage.Victorious);
		});

		it('pause sets stage to Paused', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			engine.pause();
			expect(engine.stage).toBe(BattleStage.Paused);
		});

		it('resume returns to Active when both alive', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			engine.pause();
			engine.resume();
			expect(engine.stage).toBe(BattleStage.Active);
		});
	});

	describe('logicalUpdate', () => {
		it('logs damage messages', () => {
			engine.start();
			const enemyInstance = { id: 1, level: 1, selectedSkills: [0], attributes: [] };
			enemyLoadedCallbacks[0](enemyInstance);

			logicalUpdateCallbacks[0](500);

			expect(logMessage).toHaveBeenCalledWith(ELogType.Damage, expect.stringContaining('Slash'));
		});

		it('does not process updates when not Active', () => {
			engine.start();

			logicalUpdateCallbacks[0](500);

			expect(logMessage).not.toHaveBeenCalledWith(ELogType.Damage, expect.any(String));
		});

		it('accumulates timeElapsed', () => {
			engine.start();
			logicalUpdateCallbacks[0](100);
			logicalUpdateCallbacks[0](200);

			expect(engine.timeElapsed).toBe(300);
		});
	});

	describe('getOpponent', () => {
		it('returns enemy when given player', () => {
			engine.start();
			expect(engine.getOpponent(engine.player)).toBe(engine.enemy);
		});

		it('returns player when given enemy', () => {
			engine.start();
			expect(engine.getOpponent(engine.enemy)).toBe(engine.player);
		});
	});
});
