import { ApiRequest } from './api-request';
import type { IDeviceInfoRequest } from './types';
import type { NavigatorWithCapabilities } from './navigator';

/**
 * Reports the device capabilities that aren't carried on a regular request (and aren't part of the
 * fingerprint header): `deviceMemory` and `hardwareConcurrency`. Sent once after login to enrich the
 * device record — the device itself is identified by the fingerprint header the request layer attaches.
 */

/** Reads the JS-only device capabilities from the navigator. */
export const collectDeviceInfo = (): IDeviceInfoRequest => {
	const nav = navigator as NavigatorWithCapabilities;
	return {
		deviceMemory: nav.deviceMemory,
		hardwareConcurrency: nav.hardwareConcurrency
	};
};

/**
 * Reports the current device's capabilities to the backend. Best-effort and fire-and-forget: any
 * failure (offline, missing fingerprint) is swallowed so it never disrupts the login flow. Must be
 * called after the access token is stored, since the endpoint requires authentication.
 */
export const reportDeviceInfo = async (): Promise<void> => {
	try {
		await new ApiRequest('Auth/DeviceInfo').post(collectDeviceInfo());
	} catch {
		// Tracking is non-essential; ignore failures.
	}
};
