import { SvelteMap } from 'svelte/reactivity';

/**
 * Registry of tour anchor targets, keyed by a stable string (`anchorKey`) rather than a DOM selector —
 * lesson steps reference the key, so the target markup can move/restyle without breaking a tour.
 * Registration goes through the {@link tutorialAnchor} action; {@link TourPlayer} reads it reactively
 * via {@link getTutorialAnchor} to resolve (or fail to resolve) the current step's target.
 *
 * Keyed by string rather than a per-registration id (mirroring `tooltipsData`'s id-keyed map): unlike
 * the tooltip case, only one element should ever claim a given anchor key at a time, so the destroy
 * guard below (only unregister if this node is still the one on file) is the simpler analogue of that
 * same overlapping-mount/unmount hazard.
 */
const anchors = new SvelteMap<string, HTMLElement>();

/** The registered element for `key`, or `undefined` if nothing (yet) claims it. Reactive. */
export const getTutorialAnchor = (key: string): HTMLElement | undefined => anchors.get(key);

/**
 * Registers `node` as the tour anchor target for `key` for as long as the element using the action
 * stays mounted. Attach with `use:tutorialAnchor={'skill-bar'}` on any real UI control a tour step
 * should point at.
 */
export function tutorialAnchor(node: HTMLElement, key: string) {
	let currentKey = key;
	anchors.set(currentKey, node);

	return {
		update(nextKey: string) {
			if (nextKey === currentKey) {
				return;
			}
			if (anchors.get(currentKey) === node) {
				anchors.delete(currentKey);
			}
			currentKey = nextKey;
			anchors.set(currentKey, node);
		},
		destroy() {
			// Only clear the slot if it's still this element — a second mount for the same key (e.g. an
			// overlapping screen transition) may have already replaced it, and that registration owns the
			// slot now.
			if (anchors.get(currentKey) === node) {
				anchors.delete(currentKey);
			}
		}
	};
}

type Side = 'player' | 'enemy';

/**
 * Canonical builders for the per-side fight-screen anchor keys — the single source of truth both the
 * `use:tutorialAnchor` call sites (`CombatFloaters`, `BattlerCard`, `Skills`) and `TOUR_ANCHOR_KEYS`
 * below build from, so a lesson's `anchorKey` can be validated against real registrations without
 * inspecting Svelte templates.
 */
export const TOUR_ANCHOR_KEY = {
	fightCombatLog: (side: Side) => `fight-combat-log-${side}` as const,
	fightHpBar: (side: Side) => `fight-hp-bar-${side}` as const,
	fightSkillBar: (side: Side) => `fight-skill-bar-${side}` as const
};

const SIDES: readonly Side[] = ['player', 'enemy'];

/** Every anchor key the app ever registers, across both sides. */
export const TOUR_ANCHOR_KEYS: readonly string[] = SIDES.flatMap((side) => [
	TOUR_ANCHOR_KEY.fightCombatLog(side),
	TOUR_ANCHOR_KEY.fightHpBar(side),
	TOUR_ANCHOR_KEY.fightSkillBar(side)
]);
