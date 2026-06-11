import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ApiRequest, type IPlayerChallenge } from '$lib/api';
import { playerChallenges } from '$stores/challenges.svelte';

const challenge = (challengeId: number, completed: boolean): IPlayerChallenge => ({
	challengeId,
	progress: completed ? 1 : 0,
	completed
});

describe('challenges store', () => {
	let get: ReturnType<typeof vi.spyOn>;

	beforeEach(() => {
		playerChallenges.reset();
		get = vi.spyOn(ApiRequest, 'get');
		get.mockReset();
	});

	it('loads the player challenges once and exposes them', async () => {
		get.mockResolvedValue([challenge(3, true)]);

		await playerChallenges.load();

		expect(playerChallenges.loaded).toBe(true);
		expect(playerChallenges.all).toEqual([challenge(3, true)]);

		// Idempotent: a second (non-forced) load does not re-fetch.
		await playerChallenges.load();
		expect(get).toHaveBeenCalledTimes(1);
	});

	it('re-fetches when forced', async () => {
		get.mockResolvedValue([]);
		await playerChallenges.load();

		get.mockResolvedValue([challenge(3, true)]);
		await playerChallenges.load(true);

		expect(get).toHaveBeenCalledTimes(2);
		expect(playerChallenges.all).toEqual([challenge(3, true)]);
	});

	it('coalesces concurrent loads onto a single request', async () => {
		get.mockResolvedValue([]);

		await Promise.all([playerChallenges.load(), playerChallenges.load()]);

		expect(get).toHaveBeenCalledTimes(1);
	});

	it('flags an error and leaves challenges empty when the fetch fails', async () => {
		get.mockRejectedValue(new Error('boom'));

		await playerChallenges.load();

		expect(playerChallenges.error).toBe(true);
		expect(playerChallenges.all).toEqual([]);
		expect(playerChallenges.loaded).toBe(false);
	});

	it('reports whether a given challenge is completed', async () => {
		get.mockResolvedValue([challenge(3, true), challenge(4, false)]);
		await playerChallenges.load();

		expect(playerChallenges.isChallengeCompleted(3)).toBe(true);
		expect(playerChallenges.isChallengeCompleted(4)).toBe(false); // recorded but not completed
		expect(playerChallenges.isChallengeCompleted(5)).toBe(false); // no row
	});

	describe('markCompleted', () => {
		it('flips an existing recorded challenge to completed', async () => {
			get.mockResolvedValue([challenge(3, false)]);
			await playerChallenges.load();

			playerChallenges.markCompleted(3);

			expect(playerChallenges.isChallengeCompleted(3)).toBe(true);
		});

		it('adds a completion for a challenge with no recorded progress yet', () => {
			playerChallenges.markCompleted(8);

			expect(playerChallenges.isChallengeCompleted(8)).toBe(true);
		});

		it('is a no-op when the challenge is already completed (no duplicate row)', async () => {
			get.mockResolvedValue([challenge(3, true)]);
			await playerChallenges.load();

			playerChallenges.markCompleted(3);

			expect(playerChallenges.all.filter((c) => c.challengeId === 3)).toHaveLength(1);
		});
	});

	it('reset() clears state and allows a fresh load', async () => {
		get.mockResolvedValue([challenge(3, true)]);
		await playerChallenges.load();

		playerChallenges.reset();
		expect(playerChallenges.loaded).toBe(false);
		expect(playerChallenges.all).toEqual([]);

		get.mockResolvedValue([challenge(4, true)]);
		await playerChallenges.load();
		expect(playerChallenges.isChallengeCompleted(4)).toBe(true);
	});
});
