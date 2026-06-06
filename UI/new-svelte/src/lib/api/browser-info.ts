import { ApiRequest } from './api-request';
import type { IBrowserInfoRequest } from './types';

/**
 * Device/browser signals that aren't carried on a regular request (the user-agent and client-hint
 * headers are read server-side from the request itself). These are gathered client-side and reported
 * once after login so the backend can enrich the stored browser profile (see issue #29).
 */

interface NavigatorWithCapabilities extends Navigator {
	deviceMemory?: number;
}

/**
 * The stable client-side signals that make up the device fingerprint. Kept deterministic (no
 * timestamps/randomness) so the same device produces the same hash across sessions.
 */
const fingerprintSignals = (): string => {
	const nav = navigator as NavigatorWithCapabilities;
	const timeZone = (() => {
		try {
			return Intl.DateTimeFormat().resolvedOptions().timeZone;
		} catch {
			return '';
		}
	})();

	return [
		nav.userAgent,
		nav.language,
		(nav.languages ?? []).join(','),
		nav.hardwareConcurrency ?? '',
		nav.deviceMemory ?? '',
		screen.width,
		screen.height,
		screen.colorDepth,
		timeZone,
		new Date().getTimezoneOffset()
	].join('|');
};

/**
 * Hashes the fingerprint signals with SHA-256, returning a hex string. Returns undefined when the Web
 * Crypto API is unavailable (e.g. a non-secure context), in which case the fingerprint is simply omitted.
 */
const hashFingerprint = async (input: string): Promise<string | undefined> => {
	if (!globalThis.crypto?.subtle) {
		return undefined;
	}

	try {
		const bytes = new TextEncoder().encode(input);
		const digest = await crypto.subtle.digest('SHA-256', bytes);
		return Array.from(new Uint8Array(digest))
			.map((b) => b.toString(16).padStart(2, '0'))
			.join('');
	} catch {
		return undefined;
	}
};

/** Collects the device signals (capabilities + fingerprint hash) to report after login. */
export const collectBrowserInfo = async (): Promise<IBrowserInfoRequest> => {
	const nav = navigator as NavigatorWithCapabilities;
	return {
		deviceFingerprintHash: await hashFingerprint(fingerprintSignals()),
		deviceMemory: nav.deviceMemory,
		hardwareConcurrency: nav.hardwareConcurrency
	};
};

/**
 * Reports the current device's browser info to the backend. Best-effort and fire-and-forget: any
 * failure (offline, unsupported API) is swallowed so it never disrupts the login flow. Must be called
 * after the access token is stored, since the endpoint requires authentication.
 */
export const reportBrowserInfo = async (): Promise<void> => {
	try {
		const info = await collectBrowserInfo();
		await new ApiRequest('Login/BrowserInfo').post(info);
	} catch {
		// Tracking is non-essential; ignore failures.
	}
};
