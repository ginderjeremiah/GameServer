import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import type { IAttribute, IEnemy, IZone } from '$lib/api';

// Fight composes the screen from the live battle engine (the two battlers), the zone nav
// (player manager + reference data) and the boss affordance (engine boss mode + the per-zone
// cleared statistic); all data sources are mocked.
const {
	mockBattleEngine,
	mockPlayerManager,
	mockEnemyManager,
	staticData,
	statistics,
	playerChallenges,
	registerTooltipComponent
} = vi.hoisted(() => ({
	mockBattleEngine: { player: undefined as unknown, enemy: undefined as unknown, getOpponent: vi.fn() },
	mockPlayerManager: { currentZone: 0 },
	mockEnemyManager: {
		mode: 'idle' as 'idle' | 'boss',
		autoFight: false,
		bossOutcome: undefined as 'victory' | undefined,
		bossUnlockedNextZone: false,
		challengeBoss: vi.fn(),
		retreatFromBoss: vi.fn(),
		setAutoFight: vi.fn()
	},
	staticData: { attributes: [] as IAttribute[], zones: [] as IZone[], enemies: [] as IEnemy[] },
	statistics: { isZoneCleared: vi.fn(() => false) },
	playerChallenges: { isChallengeCompleted: vi.fn(() => false) },
	registerTooltipComponent: vi.fn(() => ({
		setTooltipPosition: vi.fn(),
		showTooltip: vi.fn(),
		hideTooltip: vi.fn()
	}))
}));

vi.mock('$lib/engine', () => ({
	battleEngine: mockBattleEngine,
	playerManager: mockPlayerManager,
	enemyManager: mockEnemyManager
}));
vi.mock('$stores', () => ({ staticData, statistics, playerChallenges, registerTooltipComponent }));

import Fight from '$routes/game/screens/fight/Fight.svelte';
import { makeBattler } from './fight-fixtures';

const makeZone = (over: Partial<IZone> = {}): IZone => ({
	// Zone id == its index in staticData.zones (the Id-as-index invariant the boss view indexes by).
	id: 0,
	name: 'Verdant Hollow',
	description: '',
	order: 1,
	levelMin: 1,
	levelMax: 10,
	bossLevel: 1,
	...over
});

beforeEach(() => {
	mockBattleEngine.player = makeBattler({ name: 'Aelara' });
	mockBattleEngine.enemy = makeBattler({ name: 'Dire Wolf' });
	mockPlayerManager.currentZone = 0;
	mockEnemyManager.mode = 'idle';
	mockEnemyManager.autoFight = false;
	mockEnemyManager.bossOutcome = undefined;
	mockEnemyManager.bossUnlockedNextZone = false;
	staticData.zones = [makeZone()];
	staticData.enemies = [];
	statistics.isZoneCleared.mockReturnValue(false);
});

afterEach(cleanup);

describe('Fight', () => {
	it('lays out the player and enemy cards with a versus badge', () => {
		render(Fight);
		expect(screen.getByTestId('fight-screen')).toBeTruthy();
		expect(screen.getByTestId('vs-badge')).toBeTruthy();
		expect(screen.getByTestId('player-card')).toBeTruthy();
		expect(screen.getByTestId('enemy-card')).toBeTruthy();
	});

	it('wires each battler to its own card', () => {
		render(Fight);
		expect(screen.getByTestId('player-card').textContent).toContain('Aelara');
		expect(screen.getByTestId('enemy-card').textContent).toContain('Dire Wolf');
	});

	it('renders the zone navigation for the current zone', () => {
		render(Fight);
		expect(screen.getByTestId('zone-nav').textContent).toContain('Verdant Hollow');
	});

	it('shows no boss affordance for a zone without a dedicated boss', () => {
		render(Fight);
		expect(screen.queryByTestId('boss-trigger')).toBeNull();
		expect(screen.queryByTestId('boss-bar')).toBeNull();
	});

	describe('with a dedicated boss', () => {
		beforeEach(() => {
			staticData.zones = [makeZone({ bossEnemyId: 0, bossLevel: 18 })];
			staticData.enemies = [
				{ id: 0, name: 'Catacomb Lich', isBoss: true, attributeDistribution: [], skillPool: [], spawns: [] }
			];
		});

		it('shows the Challenge trigger naming the zone boss when available', () => {
			render(Fight);
			const trigger = screen.getByTestId('boss-trigger');
			expect(trigger.textContent).toContain('Catacomb Lich');
			expect(trigger.textContent).toContain('LV · 18');
			expect(trigger.textContent).toContain('Challenge');
			// Not engaged: no boss bar, normal enemy card.
			expect(screen.queryByTestId('boss-bar')).toBeNull();
			expect(screen.getByTestId('enemy-card')).toBeTruthy();
		});

		it('shows the Cleared seal and Re-challenge wording for a cleared zone', () => {
			statistics.isZoneCleared.mockReturnValue(true);
			render(Fight);
			const trigger = screen.getByTestId('boss-trigger');
			expect(screen.getByTestId('cleared-seal')).toBeTruthy();
			expect(trigger.textContent).toContain('Re-challenge');
		});

		it('swaps to the boss bar and the gold boss card while engaged', () => {
			mockEnemyManager.mode = 'boss';
			render(Fight);
			expect(screen.getByTestId('boss-bar')).toBeTruthy();
			expect(screen.getByTestId('boss-card')).toBeTruthy();
			// The normal enemy card is replaced by the boss card.
			expect(screen.queryByTestId('enemy-card')).toBeNull();
			expect(screen.queryByTestId('boss-trigger')).toBeNull();
		});

		it('plays the Zone-Cleared overlay on a boss victory', () => {
			mockEnemyManager.mode = 'boss';
			mockEnemyManager.bossOutcome = 'victory';
			render(Fight);
			const overlay = screen.getByTestId('zone-cleared-overlay');
			expect(overlay.textContent).toContain('Catacomb Lich defeated');
			expect(overlay.textContent).toContain('Verdant Hollow');
			// Truthfulness: this clear did not unlock a new zone, so no "next zone unlocked" claim.
			expect(screen.queryByTestId('next-zone-unlocked')).toBeNull();
		});

		it('shows the "next zone unlocked" line when the clear unlocked the next zone', () => {
			mockEnemyManager.mode = 'boss';
			mockEnemyManager.bossOutcome = 'victory';
			mockEnemyManager.bossUnlockedNextZone = true;
			render(Fight);
			expect(screen.getByTestId('next-zone-unlocked').textContent).toContain('Next zone unlocked');
		});
	});
});
