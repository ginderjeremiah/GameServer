import type { IZone } from '$lib/api';

/* Zone unlock/progression helpers. A zone is gated on a challenge: an ungated zone
   (`unlockChallengeId == null`) is always open; a gated zone unlocks once the player completes
   its gating challenge. Gating on a challenge (rather than a fixed `order - 1` chain) keeps
   progression authorable and decoupled from zone ordering. The backend enforces the same rule
   on battle start; this is the UI-side gate. */

/** Zones sorted by their authored order (defensively copied). */
export function zonesByOrder(zones: IZone[]): IZone[] {
	return zones.slice().sort((a, b) => a.order - b.order);
}

/**
 * Zones the player can navigate among: in authored order, excluding retired zones (out of
 * circulation — not navigable). A retired zone is skipped entirely rather than shown as a wall, so a
 * retired zone in the middle of the order never blocks reaching the zones beyond it. The backend
 * mirrors this: it refuses a change into a retired zone and lazily relocates a player out of one.
 */
export function navigableZones(zones: IZone[]): IZone[] {
	return zonesByOrder(zones).filter((zone) => zone.retiredAt == null);
}

/**
 * Whether a zone is unlocked for the player, given a predicate that reports challenge completion.
 * An ungated zone is always unlocked; a gated zone unlocks once its gating challenge is completed.
 */
export function isZoneUnlocked(zone: IZone, isChallengeCompleted: (challengeId: number) => boolean): boolean {
	return zone.unlockChallengeId == null || isChallengeCompleted(zone.unlockChallengeId);
}

/** The zone immediately after the given zone in authored order, or undefined if it is the last. */
export function nextZoneByOrder(zones: IZone[], currentZoneId: number): IZone | undefined {
	const ordered = zonesByOrder(zones);
	const idx = ordered.findIndex((z) => z.id === currentZoneId);
	return idx >= 0 ? ordered[idx + 1] : undefined;
}
