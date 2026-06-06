import { describe, it, expect, vi, beforeEach } from 'vitest';

const postMock = vi.fn();
const constructorMock = vi.fn();

vi.mock('$lib/api/api-request', () => ({
	ApiRequest: class {
		constructor(endpoint: string) {
			constructorMock(endpoint);
		}
		post = postMock;
	}
}));

import { collectBrowserInfo, reportBrowserInfo } from '$lib/api/browser-info';

describe('browser-info', () => {
	beforeEach(() => {
		constructorMock.mockReset();
		postMock.mockReset().mockResolvedValue({ status: 200 });
		vi.spyOn(navigator, 'hardwareConcurrency', 'get').mockReturnValue(8);
		(navigator as { deviceMemory?: number }).deviceMemory = 16;
	});

	describe('collectBrowserInfo', () => {
		it('reports device capabilities from navigator', async () => {
			const info = await collectBrowserInfo();

			expect(info.hardwareConcurrency).toBe(8);
			expect(info.deviceMemory).toBe(16);
		});

		it('produces a stable SHA-256 hex fingerprint hash', async () => {
			const first = await collectBrowserInfo();
			const second = await collectBrowserInfo();

			expect(first.deviceFingerprintHash).toBeDefined();
			// 256-bit digest rendered as hex is 64 characters.
			expect(first.deviceFingerprintHash).toMatch(/^[0-9a-f]{64}$/);
			// Deterministic: the same environment hashes to the same value.
			expect(first.deviceFingerprintHash).toBe(second.deviceFingerprintHash);
		});

		it('omits the fingerprint hash when Web Crypto is unavailable', async () => {
			const original = globalThis.crypto.subtle;
			Object.defineProperty(globalThis.crypto, 'subtle', { configurable: true, value: undefined });

			const info = await collectBrowserInfo();
			expect(info.deviceFingerprintHash).toBeUndefined();

			Object.defineProperty(globalThis.crypto, 'subtle', { configurable: true, value: original });
		});
	});

	describe('reportBrowserInfo', () => {
		it('posts the collected info to the BrowserInfo endpoint', async () => {
			await reportBrowserInfo();

			expect(constructorMock).toHaveBeenCalledWith('Login/BrowserInfo');
			expect(postMock).toHaveBeenCalledTimes(1);
			const payload = postMock.mock.calls[0][0];
			expect(payload.hardwareConcurrency).toBe(8);
			expect(payload.deviceMemory).toBe(16);
		});

		it('swallows errors so it never disrupts the login flow', async () => {
			postMock.mockRejectedValue(new Error('network'));

			await expect(reportBrowserInfo()).resolves.toBeUndefined();
		});
	});
});
