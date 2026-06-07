import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { readReferenceCache, writeReferenceCache } from '$routes/loading/reference-cache';

const STORAGE_KEY = 'gameserver.refdata.zones';

describe('reference-cache', () => {
	beforeEach(() => {
		localStorage.clear();
	});

	afterEach(() => {
		vi.restoreAllMocks();
	});

	it('returns null when nothing is cached for the key', () => {
		expect(readReferenceCache('zones')).toBeNull();
	});

	it('round-trips a versioned payload', () => {
		writeReferenceCache('zones', 'v1', [{ id: 0, name: 'Forest' }]);

		expect(readReferenceCache('zones')).toEqual({ version: 'v1', data: [{ id: 0, name: 'Forest' }] });
	});

	it('keys each set separately', () => {
		writeReferenceCache('zones', 'v1', ['z']);
		writeReferenceCache('enemies', 'v2', ['e']);

		expect(readReferenceCache('zones')).toEqual({ version: 'v1', data: ['z'] });
		expect(readReferenceCache('enemies')).toEqual({ version: 'v2', data: ['e'] });
	});

	it('preserves a null/empty cached payload distinct from a miss', () => {
		writeReferenceCache('zones', 'v1', []);

		expect(readReferenceCache('zones')).toEqual({ version: 'v1', data: [] });
	});

	it('treats a corrupted entry as a cache miss', () => {
		localStorage.setItem(STORAGE_KEY, '{ not valid json');

		expect(readReferenceCache('zones')).toBeNull();
	});

	it('treats an entry missing its version as a cache miss', () => {
		localStorage.setItem(STORAGE_KEY, JSON.stringify({ data: ['z'] }));

		expect(readReferenceCache('zones')).toBeNull();
	});

	it('silently ignores a write that exceeds the storage quota', () => {
		vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
			throw new DOMException('QuotaExceededError');
		});

		expect(() => writeReferenceCache('zones', 'v1', ['z'])).not.toThrow();
	});

	it('degrades to a miss/no-op when storage is unavailable', () => {
		vi.stubGlobal('localStorage', undefined);

		expect(readReferenceCache('zones')).toBeNull();
		expect(() => writeReferenceCache('zones', 'v1', ['z'])).not.toThrow();

		vi.unstubAllGlobals();
	});
});
