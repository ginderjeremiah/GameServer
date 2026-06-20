import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { render, cleanup, screen, fireEvent } from '@testing-library/svelte';
import { EStatisticType, type IPlayerStatistic } from '$lib/api';
import { SERVER_STAT_TYPES } from './stat-fixtures';

// Statistics fetches values over the socket (GetPlayerStatistics) and resolves entities from staticData.
const { mockFetchSocket, mockToastError, staticData } = vi.hoisted(() => ({
	mockFetchSocket: vi.fn(),
	mockToastError: vi.fn(),
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any
}));

vi.mock('$lib/api', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$lib/api')>();
	return { ...actual, fetchSocketData: mockFetchSocket };
});
// Override staticData + toastError; keep the other real stores ($components →
// log-panel pulls in the engine, which reads the logs store).
vi.mock('$stores', async (importOriginal) => {
	const actual = await importOriginal<typeof import('$stores')>();
	return { ...actual, staticData, toastError: mockToastError };
});

import Statistics from '$routes/game/screens/stats/Statistics.svelte';
import { navigation } from '$stores';

const STATS: IPlayerStatistic[] = [
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 0, value: 300 },
	{ statisticTypeId: EStatisticType.EnemiesKilled, entityId: 1, value: 50 },
	{ statisticTypeId: EStatisticType.EnemiesKilled, value: 350 },
	{ statisticTypeId: EStatisticType.FastestVictory, entityId: 0, value: 1.8 },
	{ statisticTypeId: EStatisticType.TotalBattleTime, value: 1200 },
	{ statisticTypeId: EStatisticType.PlayerDeaths, value: 2 }
];

beforeEach(() => {
	staticData.statisticTypes = SERVER_STAT_TYPES;
	staticData.enemies = [
		{ id: 0, name: 'Cave Bat', isBoss: false },
		{ id: 1, name: 'Goblin', isBoss: false }
	];
	staticData.zones = [{ id: 0, name: 'Verdant Hollow', order: 1 }];
	staticData.skills = [{ id: 0, name: 'Cleave' }];
	mockToastError.mockClear();
	mockFetchSocket.mockResolvedValue(STATS);
	navigation.clear();
	navigation.consumePayload();
});

afterEach(() => cleanup());

describe('Statistics screen', () => {
	it('fetches real statistics and renders the by-statistic view by default', async () => {
		render(Statistics);
		expect(screen.getByTestId('statistics-screen')).toBeTruthy();
		// The Combat category is active by default → its stat cards appear once loaded.
		expect(await screen.findByText('Enemies Killed')).toBeTruthy();
		expect(screen.getByTestId('stat-card-grid')).toBeTruthy();
		expect(mockFetchSocket).toHaveBeenCalledWith('GetPlayerStatistics');
	});

	it('switches category tabs to show that category’s statistics', async () => {
		render(Statistics);
		await screen.findByText('Enemies Killed');
		await fireEvent.click(screen.getByTestId('tab-time'));
		expect(screen.getByText('Total Battle Time')).toBeTruthy();
		expect(screen.getByText('Fastest Victory')).toBeTruthy();
	});

	it('deep-links an enemy stat row into the Codex (cross-link)', async () => {
		render(Statistics);
		// Cave Bat (entity 0) is the most-killed enemy, so its row is in the Enemies Killed card
		// (stat id 1) once the values load. Clicking an entity row deep-links into the Codex dossier,
		// where the per-entity statistics live now.
		const row = await screen.findByTestId('stat-row-1-0');
		await fireEvent.click(row);
		expect(navigation.requestedScreen).toBe('codex');
	});

	it('shows a friendly empty state for a player with no statistics', async () => {
		mockFetchSocket.mockResolvedValue([]);
		render(Statistics);
		expect(await screen.findByTestId('statistics-empty')).toBeTruthy();
		expect(screen.queryByTestId('stat-card-grid')).toBeNull();
		expect(mockToastError).not.toHaveBeenCalled();
	});

	it('surfaces an error (not the empty state) when the fetch fails', async () => {
		mockFetchSocket.mockRejectedValue(new Error('network down'));
		render(Statistics);
		expect(await screen.findByTestId('statistics-error')).toBeTruthy();
		// A failed load must not masquerade as the new-player empty state.
		expect(screen.queryByTestId('statistics-empty')).toBeNull();
		expect(screen.queryByTestId('stat-card-grid')).toBeNull();
		expect(mockToastError).toHaveBeenCalledTimes(1);
	});
});
