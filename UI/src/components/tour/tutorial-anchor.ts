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
