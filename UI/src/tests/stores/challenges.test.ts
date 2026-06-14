import { describe, it, expect, beforeEach, vi } from 'vitest';

// The store reads over the socket via fetchSocketData; stub just that export while keeping the real
// IPlayerChallenge type from the barrel.
const { mockFetchSocket } = vi.hoisted(() => ({ mockFetchSocket: vi.fn() }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	return { ...actual, fetchSocketData: mockFetchSocket };
});

import { type IPlayerChallenge } from '$lib/api';
import { playerChallenges } from '$stores/challenges.svelte';

const challenge = (challengeId: number, completed: boolean): IPlayerChallenge => ({
	challengeId,
	progress: completed ? 1 : 0,
	completed
});

describe('challenges store', () => {
	beforeEach(() => {
		playerChallenges.reset();
		mockFetchSocket.mockReset();
	});

	it('loads the player challenges once and exposes them', async () => {
		mockFetchSocket.mockResolvedValue([challenge(3, true)]);

		await playerChallenges.load();

		expect(playerChallenges.loaded).toBe(true);
		expect(playerChallenges.all).toEqual([challenge(3, true)]);
		expect(mockFetchSocket).toHaveBeenCalledWith('GetPlayerChallenges');

		// Idempotent: a second (non-forced) load does not re-fetch.
		await playerChallenges.load();
		expect(mockFetchSocket).toHaveBeenCalledTimes(1);
	});

	it('re-fetches when forced', async () => {
		mockFetchSocket.mockResolvedValue([]);
		await playerChallenges.load();

		mockFetchSocket.mockResolvedValue([challenge(3, true)]);
		await playerChallenges.load(true);

		expect(mockFetchSocket).toHaveBeenCalledTimes(2);
		expect(playerChallenges.all).toEqual([challenge(3, true)]);
	});

	it('coalesces concurrent loads onto a single request', async () => {
		mockFetchSocket.mockResolvedValue([]);

		await Promise.all([playerChallenges.load(), playerChallenges.load()]);

		expect(mockFetchSocket).toHaveBeenCalledTimes(1);
	});

	it('flags an error and leaves challenges empty when the fetch fails', async () => {
		mockFetchSocket.mockRejectedValue(new Error('boom'));

		await playerChallenges.load();

		expect(playerChallenges.error).toBe(true);
		expect(playerChallenges.all).toEqual([]);
		expect(playerChallenges.loaded).toBe(false);
	});

	it('reports whether a given challenge is completed', async () => {
		mockFetchSocket.mockResolvedValue([challenge(3, true), challenge(4, false)]);
		await playerChallenges.load();

		expect(playerChallenges.isChallengeCompleted(3)).toBe(true);
		expect(playerChallenges.isChallengeCompleted(4)).toBe(false); // recorded but not completed
		expect(playerChallenges.isChallengeCompleted(5)).toBe(false); // no row
	});

	describe('markCompleted', () => {
		it('flips an existing recorded challenge to completed', async () => {
			mockFetchSocket.mockResolvedValue([challenge(3, false)]);
			await playerChallenges.load();

			playerChallenges.markCompleted(3);

			expect(playerChallenges.isChallengeCompleted(3)).toBe(true);
		});

		it('adds a completion for a challenge with no recorded progress yet', () => {
			playerChallenges.markCompleted(8);

			expect(playerChallenges.isChallengeCompleted(8)).toBe(true);
		});

		it('is a no-op when the challenge is already completed (no duplicate row)', async () => {
			mockFetchSocket.mockResolvedValue([challenge(3, true)]);
			await playerChallenges.load();

			playerChallenges.markCompleted(3);

			expect(playerChallenges.all.filter((c) => c.challengeId === 3)).toHaveLength(1);
		});
	});

	it('reset() clears state and allows a fresh load', async () => {
		mockFetchSocket.mockResolvedValue([challenge(3, true)]);
		await playerChallenges.load();

		playerChallenges.reset();
		expect(playerChallenges.loaded).toBe(false);
		expect(playerChallenges.all).toEqual([]);

		mockFetchSocket.mockResolvedValue([challenge(4, true)]);
		await playerChallenges.load();
		expect(playerChallenges.isChallengeCompleted(4)).toBe(true);
	});
});
