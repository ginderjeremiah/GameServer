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

import { collectDeviceInfo, reportDeviceInfo } from '$lib/api/device-info';

describe('device-info', () => {
	beforeEach(() => {
		constructorMock.mockReset();
		postMock.mockReset().mockResolvedValue({ status: 200 });
		vi.spyOn(navigator, 'hardwareConcurrency', 'get').mockReturnValue(8);
		(navigator as { deviceMemory?: number }).deviceMemory = 16;
	});

	describe('collectDeviceInfo', () => {
		it('reports device capabilities from navigator', () => {
			const info = collectDeviceInfo();

			expect(info.hardwareConcurrency).toBe(8);
			expect(info.deviceMemory).toBe(16);
		});
	});

	describe('reportDeviceInfo', () => {
		it('posts the collected capabilities to the DeviceInfo endpoint', async () => {
			await reportDeviceInfo();

			expect(constructorMock).toHaveBeenCalledWith('Auth/DeviceInfo');
			expect(postMock).toHaveBeenCalledTimes(1);
			const payload = postMock.mock.calls[0][0];
			expect(payload.hardwareConcurrency).toBe(8);
			expect(payload.deviceMemory).toBe(16);
		});

		it('swallows errors so it never disrupts the login flow', async () => {
			postMock.mockRejectedValue(new Error('network'));

			await expect(reportDeviceInfo()).resolves.toBeUndefined();
		});
	});
});
