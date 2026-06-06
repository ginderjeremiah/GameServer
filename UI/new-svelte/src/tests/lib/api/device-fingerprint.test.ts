import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

// Each test imports a fresh copy of the module so the in-memory fingerprint cache doesn't leak.
const loadModule = async () => {
	vi.resetModules();
	return import('$lib/api/device-fingerprint');
};

describe('device-fingerprint', () => {
	beforeEach(() => {
		localStorage.clear();
	});

	afterEach(() => {
		localStorage.clear();
	});

	it('is undefined until computed', async () => {
		const { getDeviceFingerprint } = await loadModule();
		expect(getDeviceFingerprint()).toBeUndefined();
	});

	it('computes a stable SHA-256 hex fingerprint and caches it', async () => {
		const { ensureDeviceFingerprint, getDeviceFingerprint } = await loadModule();
		const first = await ensureDeviceFingerprint();

		expect(first).toBeDefined();
		// 256-bit digest rendered as hex is 64 characters.
		expect(first).toMatch(/^[0-9a-f]{64}$/);
		// Cached synchronously for the request layer, and persisted to local storage.
		expect(getDeviceFingerprint()).toBe(first);
		expect(localStorage.getItem('gameserver.device-fingerprint')).toBe(first);

		// Deterministic: a second computation yields the same value.
		expect(await ensureDeviceFingerprint()).toBe(first);
	});

	it('reads a previously persisted fingerprint synchronously', async () => {
		localStorage.setItem('gameserver.device-fingerprint', 'persisted-fp');
		const { getDeviceFingerprint } = await loadModule();
		expect(getDeviceFingerprint()).toBe('persisted-fp');
	});

	it('returns undefined when Web Crypto is unavailable', async () => {
		const original = globalThis.crypto.subtle;
		Object.defineProperty(globalThis.crypto, 'subtle', { configurable: true, value: undefined });

		const { ensureDeviceFingerprint, getDeviceFingerprint } = await loadModule();
		expect(await ensureDeviceFingerprint()).toBeUndefined();
		expect(getDeviceFingerprint()).toBeUndefined();

		Object.defineProperty(globalThis.crypto, 'subtle', { configurable: true, value: original });
	});
});
