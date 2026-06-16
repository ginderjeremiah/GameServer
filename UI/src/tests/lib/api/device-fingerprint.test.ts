import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

// Each test imports a fresh copy of the module so the in-memory fingerprint cache doesn't leak.
const loadModule = async () => {
	vi.resetModules();
	return import('$lib/api/device-fingerprint');
};

// A representative desktop-Chrome UA, parameterized on the volatile version so tests can vary it.
const chromeUserAgent = (version: string): string =>
	`Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/${version} Safari/537.36`;

const ANDROID_UA =
	'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Mobile Safari/537.36';

/**
 * Computes a fresh fingerprint with the given navigator/Date signals overridden, restoring the
 * globals afterwards. `userAgentData` is always overridden (to `undefined` when omitted) so the
 * platform token derives from `userAgent` rather than any value jsdom might expose by default.
 */
const fingerprintWith = async (overrides: {
	userAgent?: string;
	userAgentData?: { platform?: string };
	timezoneOffset?: number;
}): Promise<string | undefined> => {
	const restores: Array<() => void> = [];
	const override = (target: object, key: string, value: unknown) => {
		const prior = Object.getOwnPropertyDescriptor(target, key);
		Object.defineProperty(target, key, { configurable: true, value });
		restores.push(() => {
			if (prior) {
				Object.defineProperty(target, key, prior);
			} else {
				delete (target as Record<string, unknown>)[key];
			}
		});
	};

	if (overrides.userAgent !== undefined) {
		override(navigator, 'userAgent', overrides.userAgent);
	}
	override(navigator, 'userAgentData', overrides.userAgentData);
	if (overrides.timezoneOffset !== undefined) {
		override(Date.prototype, 'getTimezoneOffset', () => overrides.timezoneOffset);
	}

	try {
		localStorage.clear();
		const { ensureDeviceFingerprint } = await loadModule();
		return await ensureDeviceFingerprint();
	} finally {
		restores.forEach((restore) => restore());
		localStorage.clear();
	}
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

	// Regression for #749: the fingerprint must not rotate on a browser auto-update.
	it('is stable across browser auto-updates (userAgent version changes)', async () => {
		const before = await fingerprintWith({ userAgent: chromeUserAgent('120.0.0.0') });
		const after = await fingerprintWith({ userAgent: chromeUserAgent('121.0.6167.85') });

		expect(before).toMatch(/^[0-9a-f]{64}$/);
		expect(before).toBe(after);
	});

	// Regression for #749: a DST shift changes getTimezoneOffset() but must not change the hash.
	it('is stable across DST shifts (timezone offset is not a signal)', async () => {
		const winter = await fingerprintWith({ timezoneOffset: 300 });
		const summer = await fingerprintWith({ timezoneOffset: 240 });

		expect(winter).toMatch(/^[0-9a-f]{64}$/);
		expect(winter).toBe(summer);
	});

	it('produces different fingerprints for different platforms', async () => {
		const windows = await fingerprintWith({ userAgent: chromeUserAgent('120.0.0.0') });
		const android = await fingerprintWith({ userAgent: ANDROID_UA });

		expect(windows).not.toBe(android);
	});

	it('prefers the UA Client Hints platform over the userAgent string', async () => {
		// An Android UA whose client hint claims Windows must resolve to the same platform token as a
		// genuine Windows UA — the hint wins — so (all else equal) the two fingerprints match.
		const fromUserAgent = await fingerprintWith({ userAgent: chromeUserAgent('120.0.0.0') });
		const fromClientHint = await fingerprintWith({
			userAgent: ANDROID_UA,
			userAgentData: { platform: 'Windows' }
		});

		expect(fromUserAgent).toBe(fromClientHint);
	});

	it('falls back to an empty platform token for an unrecognized userAgent', async () => {
		// An unknown UA hits no family branch (token ''); a known one differs, proving the token varies.
		const unknown = await fingerprintWith({ userAgent: 'TotallyUnknownAgent/1.0' });
		const windows = await fingerprintWith({ userAgent: chromeUserAgent('120.0.0.0') });

		expect(unknown).toMatch(/^[0-9a-f]{64}$/);
		expect(unknown).not.toBe(windows);
	});
});
