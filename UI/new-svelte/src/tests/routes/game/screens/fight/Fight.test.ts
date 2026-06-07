import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen } from '@testing-library/svelte';
import type { IAttribute, IZone } from '$lib/api';

// Fight composes the screen from the live battle engine (the two battlers) and the zone nav
// (player manager + reference data); all data sources are mocked.
const { mockBattleEngine, mockPlayerManager, staticData } = vi.hoisted(() => ({
	mockBattleEngine: { player: undefined as unknown, enemy: undefined as unknown, getOpponent: vi.fn() },
	mockPlayerManager: { currentZone: 0 },
	staticData: { attributes: [] as IAttribute[], zones: [] as IZone[] }
}));

vi.mock('$lib/engine', () => ({ battleEngine: mockBattleEngine, playerManager: mockPlayerManager }));
vi.mock('$stores', () => ({ staticData }));

import Fight from '$routes/game/screens/fight/Fight.svelte';
import { makeBattler } from './fight-fixtures';

beforeEach(() => {
	mockBattleEngine.player = makeBattler({ name: 'Aelara' });
	mockBattleEngine.enemy = makeBattler({ name: 'Dire Wolf' });
	mockPlayerManager.currentZone = 10;
	staticData.zones = [{ id: 10, name: 'Verdant Hollow', description: '', order: 1, levelMin: 1, levelMax: 10 }];
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
});
