import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ApiRequest, EStatisticType, type IPlayerStatistic } from '$lib/api';
import { statistics } from '$stores/statistics.svelte';

const zonesCleared = (entityId: number, value: number): IPlayerStatistic => ({
	statisticTypeId: EStatisticType.ZonesCleared,
	entityId,
	value
});

describe('statistics store', () => {
	let get: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		statistics.reset();
		get = vi.spyOn(ApiRequest, 'get');
		get.mockReset();
	});

	it('loads the player statistics once and exposes them', async () => {
		get.mockResolvedValue([zonesCleared(3, 1)]);

		await statistics.load();

		expect(statistics.loaded).toBe(true);
		expect(statistics.stats).toEqual([zonesCleared(3, 1)]);

		// Idempotent: a second (non-forced) load does not re-fetch.
		await statistics.load();
		expect(get).toHaveBeenCalledTimes(1);
	});

	it('re-fetches when forced', async () => {
		get.mockResolvedValue([]);
		await statistics.load();

		get.mockResolvedValue([zonesCleared(3, 1)]);
		await statistics.load(true);

		expect(get).toHaveBeenCalledTimes(2);
		expect(statistics.stats).toEqual([zonesCleared(3, 1)]);
	});

	it('coalesces concurrent loads onto a single request', async () => {
		get.mockResolvedValue([]);

		await Promise.all([statistics.load(), statistics.load()]);

		expect(get).toHaveBeenCalledTimes(1);
	});

	it('flags an error and leaves stats empty when the fetch fails', async () => {
		get.mockRejectedValue(new Error('boom'));

		await statistics.load();

		expect(statistics.error).toBe(true);
		expect(statistics.stats).toEqual([]);
		expect(statistics.loaded).toBe(false);
	});

	it('reports whether a given zone is cleared', async () => {
		get.mockResolvedValue([zonesCleared(3, 1), zonesCleared(4, 0)]);
		await statistics.load();

		expect(statistics.isZoneCleared(3)).toBe(true);
		expect(statistics.isZoneCleared(4)).toBe(false); // recorded but value 0
		expect(statistics.isZoneCleared(5)).toBe(false); // no row
	});

	it('optimistically marks a zone cleared by adding a row', async () => {
		get.mockResolvedValue([]);
		await statistics.load();

		statistics.markZoneCleared(3);

		expect(statistics.isZoneCleared(3)).toBe(true);
	});

	it('optimistically promotes an existing uncleared row without duplicating it', async () => {
		get.mockResolvedValue([zonesCleared(3, 0)]);
		await statistics.load();

		statistics.markZoneCleared(3);

		expect(statistics.isZoneCleared(3)).toBe(true);
		expect(statistics.stats.filter((s) => s.entityId === 3)).toHaveLength(1);
	});

	it('reset() clears state and allows a fresh load', async () => {
		get.mockResolvedValue([zonesCleared(3, 1)]);
		await statistics.load();

		statistics.reset();
		expect(statistics.loaded).toBe(false);
		expect(statistics.stats).toEqual([]);

		get.mockResolvedValue([zonesCleared(4, 1)]);
		await statistics.load();
		expect(statistics.isZoneCleared(4)).toBe(true);
	});
});
