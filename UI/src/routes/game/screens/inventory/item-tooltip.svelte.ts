import { getContext, setContext } from 'svelte';
import type { Item } from '$lib/battle';
import { anchorPosition, registerTooltipComponent, type TooltipAnchor, type TooltipComponent } from '$stores';

/**
 * Imperative controller for the inventory's single shared `ItemTooltip` — opens it for a given item,
 * repositions it as the cursor moves, and hides it. Shared down the inventory screen via
 * {@link setItemTooltip} context so the equipped rail and the item grid drive one panel instead of
 * each mounting their own, mirroring the attribute-tooltip controller.
 *
 * `show`/`move` accept a {@link TooltipAnchor} (a pointer event positioned at the cursor, or an
 * element positioned off its box) so the tooltip stays reachable by mouse and keyboard alike.
 */
export interface ItemTooltipController {
	show: (item: Item, anchor: TooltipAnchor) => void;
	move: (anchor: TooltipAnchor) => void;
	hide: () => void;
}

/** The reactive render state of the tooltip (the hovered `item`) plus its {@link ItemTooltipController}. */
export interface ItemTooltipHandle {
	readonly controller: ItemTooltipController;
	readonly item: Item | undefined;
}

/**
 * Registers a single item tooltip with the global tooltip store and returns the reactive render
 * state (the hovered `item`) plus an imperative {@link ItemTooltipController}. The owning screen
 * renders `<ItemTooltip item={handle.item} bind:this={component} />` and publishes the controller via
 * {@link setItemTooltip} so the equipped rail and the item grid drive it from their hover handlers.
 *
 * Must be called during component initialisation (it registers an `onDestroy` cleanup).
 */
export function createItemTooltip(getComponent: () => TooltipComponent | undefined): ItemTooltipHandle {
	let item = $state<Item | undefined>();
	const { setTooltipPosition, showTooltip, hideTooltip } = registerTooltipComponent(getComponent);

	const controller: ItemTooltipController = {
		show(next, anchor) {
			item = next;
			setTooltipPosition(anchorPosition(anchor));
			showTooltip();
		},
		move(anchor) {
			setTooltipPosition(anchorPosition(anchor));
		},
		hide() {
			item = undefined;
			hideTooltip();
		}
	};

	return {
		controller,
		get item() {
			return item;
		}
	};
}

const ITEM_TOOLTIP_KEY = Symbol('item-tooltip');

/** Publish a controller to the inventory's hover surfaces (call from the screen that owns the panel). */
export const setItemTooltip = (controller: ItemTooltipController): void => {
	setContext(ITEM_TOOLTIP_KEY, controller);
};

/** The nearest ancestor's item-tooltip controller, or `undefined` if no screen provides one. */
export const getItemTooltip = (): ItemTooltipController | undefined =>
	getContext<ItemTooltipController>(ITEM_TOOLTIP_KEY);
