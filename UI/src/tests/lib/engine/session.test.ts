import { describe, it, expect, vi, beforeEach } from 'vitest';

const { getTokens, reportDeviceInfo, statusGet, playerInitialize, hydrateAllFromCache } = vi.hoisted(() => ({
	getTokens: vi.fn(),
	reportDeviceInfo: vi.fn(),
	statusGet: vi.fn(),
	playerInitialize: vi.fn(),
	hydrateAllFromCache: vi.fn()
}));

vi.mock('$lib/api', () => ({
	getTokens,
	reportDeviceInfo,
	ApiRequest: class {
		get = statusGet;
	}
}));
vi.mock('$lib/engine/player/player-manager', () => ({ playerManager: { initialize: playerInitialize } }));
vi.mock('$lib/engine/reference-data', () => ({ hydrateAllFromCache }));

import { resumeSession } from '$lib/engine/session';

const PLAYER = { name: 'Hero' };

beforeEach(() => {
	vi.clearAllMocks();
	getTokens.mockReturnValue({ accessToken: 'a', refreshToken: 'r' });
	statusGet.mockResolvedValue({ status: 200, data: PLAYER });
	hydrateAllFromCache.mockResolvedValue(true);
});

describe('resumeSession', () => {
	it('returns "login" and makes no request when there are no stored tokens', async () => {
		getTokens.mockReturnValue(null);

		expect(await resumeSession()).toBe('login');
		expect(statusGet).not.toHaveBeenCalled();
		expect(hydrateAllFromCache).not.toHaveBeenCalled();
	});

	it('returns "login" without restoring the player when the status check is not 200', async () => {
		statusGet.mockResolvedValue({ status: 401 });

		expect(await resumeSession()).toBe('login');
		expect(playerInitialize).not.toHaveBeenCalled();
		expect(hydrateAllFromCache).not.toHaveBeenCalled();
	});

	it('returns "login" when the status request throws', async () => {
		statusGet.mockRejectedValue(new Error('network'));

		expect(await resumeSession()).toBe('login');
		expect(playerInitialize).not.toHaveBeenCalled();
	});

	it('returns "login" when restoring the player from a 200 response throws', async () => {
		// A 200 response whose body can't be turned into a player (e.g. an unparseable/malformed payload):
		// the throw originates after the status check, so the catch must still route to login, not crash boot.
		// mockImplementationOnce keeps the throw scoped to restorePlayer's single call (clearAllMocks in the
		// shared beforeEach resets call records but not a persistent implementation).
		playerInitialize.mockImplementationOnce(() => {
			throw new Error('bad player payload');
		});

		expect(await resumeSession()).toBe('login');
		expect(hydrateAllFromCache).not.toHaveBeenCalled();
	});

	it('restores the player and returns "game" when the whole reference cache is current', async () => {
		expect(await resumeSession()).toBe('game');
		expect(playerInitialize).toHaveBeenCalledWith(PLAYER);
		expect(reportDeviceInfo).toHaveBeenCalledTimes(1);
		expect(hydrateAllFromCache).toHaveBeenCalledTimes(1);
	});

	it('restores the player but returns "loading" when a reference set must be downloaded', async () => {
		hydrateAllFromCache.mockResolvedValue(false);

		expect(await resumeSession()).toBe('loading');
		expect(playerInitialize).toHaveBeenCalledWith(PLAYER);
	});
});
