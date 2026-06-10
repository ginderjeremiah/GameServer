import { describe, it, expect, beforeEach, vi } from 'vitest';
import type { IEnemy, IZone } from '$lib/api';

// BossView derives its state from the engine's boss mode (enemyManager), the current zone's
// authored boss (staticData), and the per-zone cleared statistic — all mocked here. Each test
// sets the mock state and constructs a fresh view (the $derived reads the current values).
const { enemyManager, playerManager, staticData, statistics } = vi.hoisted(() => ({
	enemyManager: {
		mode: 'idle' as 'idle' | 'boss',
		autoFight: false,
		bossOutcome: undefined as 'victory' | undefined,
		challengeBoss: vi.fn(),
		retreatFromBoss: vi.fn(),
		setAutoFight: vi.fn()
	},
	playerManager: { currentZone: 0 },
	staticData: { zones: [] as IZone[], enemies: [] as IEnemy[] },
	statistics: { isZoneCleared: vi.fn(() => false) }
}));

vi.mock('$lib/engine', () => ({ enemyManager, playerManager }));
vi.mock('$stores', () => ({ staticData, statistics }));

import { BossView } from '$routes/game/screens/fight/boss/boss-view.svelte';

const bossZone: IZone = {
	id: 3,
	name: 'Forgotten Catacombs',
	description: '',
	order: 3,
	levelMin: 8,
	levelMax: 11,
	bossEnemyId: 0,
	bossLevel: 18
};
const bosslessZone: IZone = {
	id: 1,
	name: 'Verdant Hollow',
	description: '',
	order: 1,
	levelMin: 1,
	levelMax: 10,
	bossLevel: 1
};
const boss: IEnemy = {
	id: 0,
	name: 'Catacomb Lich',
	isBoss: true,
	attributeDistribution: [],
	skillPool: [],
	spawns: []
};

beforeEach(() => {
	enemyManager.mode = 'idle';
	enemyManager.autoFight = false;
	enemyManager.bossOutcome = undefined;
	enemyManager.challengeBoss.mockReset();
	enemyManager.retreatFromBoss.mockReset();
	enemyManager.setAutoFight.mockReset();
	playerManager.currentZone = 3;
	// Reference sets are indexed by id (the zero-based Id-as-index invariant), so each
	// zone sits at its own id; ids 1 and 3 leave holes that index access skips as undefined.
	const zones: IZone[] = [];
	zones[bosslessZone.id] = bosslessZone;
	zones[bossZone.id] = bossZone;
	staticData.zones = zones;
	staticData.enemies = [boss];
	statistics.isZoneCleared.mockReturnValue(false);
});

describe('BossView', () => {
	it('resolves the current zone boss, name, level and zone name', () => {
		const view = new BossView();
		expect(view.hasBoss).toBe(true);
		expect(view.boss).toEqual(boss);
		expect(view.bossName).toBe('Catacomb Lich');
		expect(view.bossLevel).toBe(18);
		expect(view.zoneName).toBe('Forgotten Catacombs');
	});

	it('reports no boss for a zone without one', () => {
		playerManager.currentZone = 1;
		const view = new BossView();
		expect(view.hasBoss).toBe(false);
		expect(view.boss).toBeUndefined();
		expect(view.bossName).toBe('Zone Boss');
	});

	it('tracks the engine engaged / auto-fight / victory state', () => {
		enemyManager.mode = 'boss';
		enemyManager.autoFight = true;
		enemyManager.bossOutcome = 'victory';
		const view = new BossView();
		expect(view.engaged).toBe(true);
		expect(view.autoFight).toBe(true);
		expect(view.victory).toBe(true);
	});

	it('only surfaces victory while engaged', () => {
		enemyManager.mode = 'idle';
		enemyManager.bossOutcome = 'victory';
		const view = new BossView();
		expect(view.victory).toBe(false);
	});

	it('reflects the per-zone cleared statistic for the current zone', () => {
		statistics.isZoneCleared.mockReturnValue(true);
		const view = new BossView();
		expect(view.cleared).toBe(true);
		expect(statistics.isZoneCleared).toHaveBeenCalledWith(3);
	});

	it('proxies the player actions to the engine', () => {
		const view = new BossView();
		view.challenge();
		view.retreat();
		view.toggleAutoFight(true);
		expect(enemyManager.challengeBoss).toHaveBeenCalledOnce();
		expect(enemyManager.retreatFromBoss).toHaveBeenCalledOnce();
		expect(enemyManager.setAutoFight).toHaveBeenCalledWith(true);
	});
});
