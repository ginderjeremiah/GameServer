/**
 * Augments the standard `Navigator` with optional capability fields the DOM lib types don't model:
 * the Device Memory API's `deviceMemory` and the UA Client Hints `userAgentData` (we only read its
 * stable `platform`). Shared by the device fingerprint and the device-info collector.
 */
export interface NavigatorWithCapabilities extends Navigator {
	deviceMemory?: number;
	userAgentData?: { platform?: string };
}
