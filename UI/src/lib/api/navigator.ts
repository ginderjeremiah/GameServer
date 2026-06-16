/**
 * Augments the standard `Navigator` with the optional Device Memory API field (`deviceMemory`), which
 * the DOM lib types don't model. Shared by the device fingerprint and the device-info collector.
 */
export interface NavigatorWithCapabilities extends Navigator {
	deviceMemory?: number;
}
