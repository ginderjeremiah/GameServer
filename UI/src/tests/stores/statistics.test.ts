import { describe, it, expect, beforeEach, vi } from 'vitest';

// The store reads over the socket via fetchSocketData; stub just that export while keeping the real
// EStatisticType/IPlayerStatistic from the barrel so the assertions use the genuine enum values.
const { mockFetchSocket } = vi.hoisted(() => ({ mockFetchSocket: vi.fn() }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, fetchSocketData: mockFetchSocket };
});

import { EStatisticType, type IPlayerStatistic } from '$lib/api';
import { statistics } from '$stores/statistics.svelte';

const zonesCleared = (entityId: number, value: number): IPlayerStatistic => ({
	statisticTypeId: EStatisticType.ZonesCleared,
	entityId,
	value
});

describe('statistics store', () => {
	beforeEach(() => {
		statistics.reset();
		mockFetchSocket.mockReset();
	});

	it('loads the player statistics once and exposes them', async () => {
		mockFetchSocket.mockResolvedValue([zonesCleared(3, 1)]);

		await statistics.load();

		expect(statistics.loaded).toBe(true);
		expect(statistics.stats).toEqual([zonesCleared(3, 1)]);
		expect(mockFetchSocket).toHaveBeenCalledWith('GetPlayerStatistics');

		// Idempotent: a second (non-forced) load does not re-fetch.
		await statistics.load();
		expect(mockFetchSocket).toHaveBeenCalledTimes(1);
	});

	it('re-fetches when forced', async () => {
		mockFetchSocket.mockResolvedValue([]);
		await statistics.load();

		mockFetchSocket.mockResolvedValue([zonesCleared(3, 1)]);
		await statistics.load(true);

		expect(mockFetchSocket).toHaveBeenCalledTimes(2);
		expect(statistics.stats).toEqual([zonesCleared(3, 1)]);
	});

	it('coalesces concurrent loads onto a single request', async () => {
		mockFetchSocket.mockResolvedValue([]);

		await Promise.all([statistics.load(), statistics.load()]);

		expect(mockFetchSocket).toHaveBeenCalledTimes(1);
	});

	it('flags an error and leaves stats empty when the fetch fails', async () => {
		mockFetchSocket.mockRejectedValue(new Error('boom'));

		await statistics.load();

		expect(statistics.error).toBe(true);
		expect(statistics.stats).toEqual([]);
		expect(statistics.loaded).toBe(false);
	});

	it('reports whether a given zone is cleared', async () => {
		mockFetchSocket.mockResolvedValue([zonesCleared(3, 1), zonesCleared(4, 0)]);
		await statistics.load();

		expect(statistics.isZoneCleared(3)).toBe(true);
		expect(statistics.isZoneCleared(4)).toBe(false); // recorded but value 0
		expect(statistics.isZoneCleared(5)).toBe(false); // no row
	});

	it('optimistically marks a zone cleared by adding a row', async () => {
		mockFetchSocket.mockResolvedValue([]);
		await statistics.load();

		statistics.markZoneCleared(3);

		expect(statistics.isZoneCleared(3)).toBe(true);
	});

	it('optimistically promotes an existing uncleared row without duplicating it', async () => {
		mockFetchSocket.mockResolvedValue([zonesCleared(3, 0)]);
		await statistics.load();

		statistics.markZoneCleared(3);

		expect(statistics.isZoneCleared(3)).toBe(true);
		expect(statistics.stats.filter((s) => s.entityId === 3)).toHaveLength(1);
	});

	it('reset() clears state and allows a fresh load', async () => {
		mockFetchSocket.mockResolvedValue([zonesCleared(3, 1)]);
		await statistics.load();

		statistics.reset();
		expect(statistics.loaded).toBe(false);
		expect(statistics.stats).toEqual([]);

		mockFetchSocket.mockResolvedValue([zonesCleared(4, 1)]);
		await statistics.load();
		expect(statistics.isZoneCleared(4)).toBe(true);
	});
});
