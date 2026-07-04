import { describe, it, expect, beforeEach, vi } from 'vitest';

/*
 * Integration-style coverage of WorkbenchReference.load() — the out-of-process
 * orchestration #320 deferred. Rather than mock load() into a tautology, the real
 * method runs against stubbed transports so the assertions verify the documented
 * socket-vs-HTTP split (reference catalogues over the `Get*` socket commands; only
 * tags/categories over HTTP, since they have no socket command yet) and that the
 * results land where the workbench reads them, with `loaded` flipping true.
 */

const { staticData, mockFetchSocket, mockGet } = vi.hoisted(() => ({
	// eslint-disable-next-line @typescript-eslint/no-explicit-any
	staticData: {} as any,
	mockFetchSocket: vi.fn(),
	mockGet: vi.fn()
}));
vi.mock('$stores', () => ({ staticData }));
vi.mock('$lib/api', async (importOriginal) => {
	const actual = (await importOriginal()) as Record<string, unknown>;
	class ApiRequest {
		static get = mockGet;
		static post = vi.fn();
	}
	return { ...actual, ApiRequest, fetchSocketData: mockFetchSocket };
});

import { reference } from '$routes/admin/workbench/reference.svelte';

// One distinct payload per socket command, so a mis-wired assignment is caught.
const SOCKET_SETS: Record<string, { id: number; name: string }[]> = {
	GetEnemies: [{ id: 0, name: 'Cave Bat' }],
	GetSkills: [{ id: 0, name: 'Cleave' }],
	GetZones: [{ id: 0, name: 'Verdant Hollow' }],
	GetItems: [{ id: 0, name: 'Iron Helm' }],
	GetItemMods: [{ id: 0, name: 'Sharp' }],
	GetAttributes: [{ id: 0, name: 'Strength' }],
	GetChallengeTypes: [{ id: 1, name: 'Enemies Killed' }],
	GetChallenges: [{ id: 0, name: 'First Blood' }],
	GetPaths: [{ id: 0, name: 'Fire Magic' }],
	GetProficiencies: [{ id: 0, name: 'Fire' }],
	GetLessons: [{ id: 0, name: 'Idle Combat' }]
};
const TAGS = [{ id: 10, name: 'Fire', tagCategoryId: 100 }];
const TAG_CATEGORIES = [{ id: 100, name: 'Element' }];

beforeEach(() => {
	mockFetchSocket.mockReset().mockImplementation(async (command: string) => SOCKET_SETS[command]);
	mockGet
		.mockReset()
		.mockImplementation(async (path: string) =>
			path === 'Tags' ? TAGS : path === 'Tags/TagCategories' ? TAG_CATEGORIES : []
		);
	for (const key of Object.keys(staticData)) {
		delete staticData[key];
	}
});

describe('WorkbenchReference.load', () => {
	it('reads each set over its correct transport, populates the stores, and flips loaded', async () => {
		expect(reference.loaded).toBe(false);

		await reference.load();

		// The zero-based-id catalogues plus challenge types, paths and proficiencies load over the socket.
		for (const command of [
			'GetEnemies',
			'GetSkills',
			'GetZones',
			'GetItems',
			'GetItemMods',
			'GetAttributes',
			'GetChallengeTypes',
			'GetChallenges',
			'GetPaths',
			'GetProficiencies',
			'GetLessons'
		]) {
			expect(mockFetchSocket).toHaveBeenCalledWith(command);
		}
		// Only tags + categories use HTTP — the transport split is exactly 11 socket / 2 HTTP.
		expect(mockFetchSocket).toHaveBeenCalledTimes(11);
		expect(mockGet).toHaveBeenCalledTimes(2);
		expect(mockGet).toHaveBeenCalledWith('Tags');
		expect(mockGet).toHaveBeenCalledWith('Tags/TagCategories');

		// The socket catalogues land in the shared staticData store…
		expect(staticData.enemies).toBe(SOCKET_SETS.GetEnemies);
		expect(staticData.skills).toBe(SOCKET_SETS.GetSkills);
		expect(staticData.zones).toBe(SOCKET_SETS.GetZones);
		expect(staticData.items).toBe(SOCKET_SETS.GetItems);
		expect(staticData.itemMods).toBe(SOCKET_SETS.GetItemMods);
		expect(staticData.attributes).toBe(SOCKET_SETS.GetAttributes);
		expect(staticData.challenges).toBe(SOCKET_SETS.GetChallenges);
		expect(staticData.paths).toBe(SOCKET_SETS.GetPaths);
		expect(staticData.proficiencies).toBe(SOCKET_SETS.GetProficiencies);
		expect(staticData.lessons).toBe(SOCKET_SETS.GetLessons);
		// …and the tags/categories/challenge-types the singleton owns onto its own fields
		// (these are $state-wrapped reactive proxies, so compare by value, not identity).
		expect(reference.tags).toEqual(TAGS);
		expect(reference.tagCategories).toEqual(TAG_CATEGORIES);
		expect(reference.challengeTypes).toEqual(SOCKET_SETS.GetChallengeTypes);

		expect(reference.loaded).toBe(true);
	});

	it('propagates a socket failure so a load error surfaces (fetchSocketData throws on socket error)', async () => {
		mockFetchSocket.mockImplementation(async (command: string) => {
			if (command === 'GetItems') {
				throw new Error('socket down');
			}
			return SOCKET_SETS[command];
		});

		await expect(reference.load()).rejects.toThrow('socket down');
	});
});
